using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Domain;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/weight-history")]
    public class PetWeightLogController : ControllerBase
    {
        private readonly IPetWeightLogService _service;

        public PetWeightLogController(IPetWeightLogService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<PetWeightLogResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(Guid petId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(TokenInvalid());

            return Ok(await _service.GetByPetIdAsync(petId, userId, from, to));
        }

        [HttpPost]
        [ProducesResponseType(typeof(PetWeightLogResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreatePetWeightLogDto dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(TokenInvalid());

            var created = await _service.CreateAsync(petId, userId, dto);
            return Created($"/api/pets/{petId}/weight-history", created);
        }

        [HttpPatch("{weightLogId:guid}")]
        [ProducesResponseType(typeof(PetWeightLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid weightLogId, [FromBody] PatchPetWeightLogDto dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(TokenInvalid());

            return Ok(await _service.UpdateAsync(petId, weightLogId, userId, dto));
        }

        [HttpDelete("{weightLogId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid weightLogId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(TokenInvalid());

            var deleted = await _service.DeleteAsync(petId, weightLogId, userId);
            if (!deleted)
                return NotFound(ApiErrorResponse.FromMessage(
                    "Weight log not found", ErrorCodes.WeightLog.NotFound));

            return NoContent();
        }

        private bool TryGetUserId(out Guid userId) =>
            Guid.TryParse(User.FindFirst("userId")?.Value, out userId);

        private static ApiErrorResponse TokenInvalid() =>
            ApiErrorResponse.FromMessage(
                "Authentication token is invalid", ErrorCodes.AuthenticationTokenInvalid);
    }
}
