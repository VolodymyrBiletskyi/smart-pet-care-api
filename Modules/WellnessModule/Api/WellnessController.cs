using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [HttpGet("evaluation")]
    [ProducesResponseType(typeof(WellnessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> GetOrCreateEvaluation(
        Guid petId,
        CancellationToken cancellationToken) =>
        RunGetOrCreateEvaluationAsync(petId, cancellationToken);

    private async Task<IActionResult> RunGetOrCreateEvaluationAsync(
        Guid petId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await wellnessService.GetOrCreateEvaluationAsync(
                petId, User.GetUserId(), cancellationToken));
        }
        catch (WellnessInsufficientDataException exception)
        {
            return StatusCode(
                StatusCodes.Status422UnprocessableEntity,
                Error(exception.Message, "wellness_insufficient_data"));
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(Error(exception.Message, "pet_not_found"));
        }
        catch (ClassifierRateLimitedException exception)
        {
            SetRetryAfter(exception.RetryAfterSeconds);
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                Error(
                    "Wellness evaluation is rate limited",
                    "wellness_service_rate_limited",
                    exception.RetryAfterSeconds));
        }
        catch (ClassifierInvalidResponseException exception)
        {
            logger.LogWarning(
                "Wellness classifier returned an invalid response for pet {PetId}: {ValidationReason}",
                petId,
                exception.ValidationReason ?? "validation reason was not provided");
            return StatusCode(
                StatusCodes.Status502BadGateway,
                Error("The wellness service returned an invalid response", "wellness_service_invalid_response"));
        }
        catch (ClassifierUnavailableException exception)
        {
            SetRetryAfter(exception.RetryAfterSeconds);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                Error(
                    "The wellness service is unavailable",
                    "wellness_service_unavailable",
                    exception.RetryAfterSeconds));
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
            return NotFound(Error(exception.Message, "pet_not_found"));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Error(exception.Message, "wellness_history_query_invalid"));
        }
    }

    private void SetRetryAfter(int? seconds)
    {
        if (seconds is >= 0) Response.Headers.RetryAfter = seconds.Value.ToString();
    }

    private static ApiErrorResponse Error(
        string message,
        string? code = null,
        int? retryAfterSeconds = null) =>
        ApiErrorResponse.FromMessage(message, code, retryAfterSeconds);
}
