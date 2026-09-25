using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/activity-logs")]
    public class ActivityLogController : ControllerBase
    {
        private readonly IActivityLogService _service;

        public ActivityLogController(IActivityLogService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ActivityLogResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(
            Guid petId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] ActivitySource? source)
        {
            var userId = User.GetUserId();

            var logs = await _service.GetByPetIdAsync(petId, userId, from, to, source);
            return Ok(logs);
        }

        [HttpGet("{activityLogId:guid}")]
        [ProducesResponseType(typeof(ActivityLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid activityLogId)
        {
            var userId = User.GetUserId();

            var log = await _service.GetByIdAsync(petId, activityLogId, userId);
            if (log is null) return NotFound(Error("Activity log not found", ErrorCodes.Activity.LogNotFound));
            return Ok(log);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ActivityLogResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateActivityLogDto dto)
        {
            var userId = User.GetUserId();

            var created = await _service.CreateAsync(petId, userId, dto);
            return CreatedAtAction(nameof(GetById), new { petId, activityLogId = created.Id }, created);
        }

        [HttpPatch("{activityLogId:guid}")]
        [ProducesResponseType(typeof(ActivityLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid activityLogId, [FromBody] PatchActivityLogDto dto)
        {
            var userId = User.GetUserId();

            var updated = await _service.UpdateAsync(petId, activityLogId, userId, dto);
            return Ok(updated);
        }

        [HttpDelete("{activityLogId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid activityLogId)
        {
            var userId = User.GetUserId();

            var deleted = await _service.DeleteAsync(petId, activityLogId, userId);
            if (!deleted) return NotFound(Error("Activity log not found", ErrorCodes.Activity.LogNotFound));
            return NoContent();
        }

        private static ApiErrorResponse Error(string message, string code) =>
            ApiErrorResponse.FromMessage(message, code);
    }
}


