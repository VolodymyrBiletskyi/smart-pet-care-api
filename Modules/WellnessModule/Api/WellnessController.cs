using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.WellnessModule.Domain;
using smart_pet_care_api.Modules.WellnessModule.DTOs;

namespace smart_pet_care_api.Modules.WellnessModule.Api;

[ApiController]
[Authorize]
[Route("api/pets/{petId:guid}/wellness")]
public sealed class WellnessController(IWellnessService wellnessService) : ControllerBase
{
    [HttpGet("evaluation")]
    [ProducesResponseType(typeof(WellnessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetOrCreateEvaluation(
        Guid petId,
        CancellationToken cancellationToken) =>
        Ok(await wellnessService.GetOrCreateEvaluationAsync(
            petId, User.GetUserId(), cancellationToken));

    [HttpGet("history")]
    [ProducesResponseType(typeof(WellnessHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(
        Guid petId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = WellnessService.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        Ok(await wellnessService.GetHistoryAsync(
            petId, User.GetUserId(), page, pageSize, cancellationToken));
}
