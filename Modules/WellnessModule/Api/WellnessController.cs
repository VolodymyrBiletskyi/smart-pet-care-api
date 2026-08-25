using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.WellnessModule.Domain;
using smart_pet_care_api.Modules.WellnessModule.DTOs;

namespace smart_pet_care_api.Modules.WellnessModule.Api;

[ApiController]
[Authorize]
[Route("api/pets/{petId:guid}/wellness")]
public sealed class WellnessController(
    IWellnessService wellnessService,
    ILogger<WellnessController> logger) : ControllerBase
{
    [HttpPost("recalculate")]
    [ProducesResponseType(typeof(WellnessAssessmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Recalculate(
        Guid petId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WellnessRecalculationRequestDto? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await wellnessService.RecalculateAsync(
                petId, User.GetUserId(), request?.CurrentSymptoms, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(Error(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Error(exception.Message));
        }
        catch (ClassifierRateLimitedException exception)
        {
            SetRetryAfter(exception.RetryAfterSeconds);
            return StatusCode(StatusCodes.Status429TooManyRequests, Error("Wellness calculation is rate limited"));
        }
        catch (ClassifierInvalidResponseException exception)
        {
            logger.LogWarning(
                "Wellness classifier returned an invalid response for pet {PetId}: {ValidationReason}",
                petId,
                exception.ValidationReason ?? "validation reason was not provided");
            return StatusCode(StatusCodes.Status502BadGateway, Error("The wellness service returned an invalid response"));
        }
        catch (ClassifierUnavailableException exception)
        {
            SetRetryAfter(exception.RetryAfterSeconds);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, Error("The wellness service is unavailable"));
        }
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(WellnessAssessmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Current(Guid petId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await wellnessService.GetCurrentAsync(
                petId, User.GetUserId(), cancellationToken);
            return result is null ? NotFound(Error("Wellness assessment not found")) : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(Error(exception.Message));
        }
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(WellnessHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(
        Guid petId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = WellnessService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await wellnessService.GetHistoryAsync(
                petId, User.GetUserId(), page, pageSize, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(Error(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Error(exception.Message));
        }
    }

    private void SetRetryAfter(int? seconds)
    {
        if (seconds is >= 0) Response.Headers.RetryAfter = seconds.Value.ToString();
    }

    private static ApiErrorResponse Error(string message) => ApiErrorResponse.FromMessage(message);
}
