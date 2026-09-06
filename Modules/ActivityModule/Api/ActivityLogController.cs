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

            try
            {
                var logs = await _service.GetByPetIdAsync(petId, userId, from, to, source);
                return Ok(logs);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(Error(ex.Message));
            }
        }

        [HttpGet("{activityLogId:guid}")]
        [ProducesResponseType(typeof(ActivityLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid activityLogId)
        {
            var userId = User.GetUserId();

            try
            {
                var log = await _service.GetByIdAsync(petId, activityLogId, userId);
                if (log is null) return NotFound(Error("Activity log not found"));
                return Ok(log);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message));
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(ActivityLogResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateActivityLogDto dto)
        {
            var userId = User.GetUserId();

            try
            {
                var created = await _service.CreateAsync(petId, userId, dto);
                return CreatedAtAction(nameof(GetById), new { petId, activityLogId = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(Error(ex.Message));
            }
        }

        [HttpPatch("{activityLogId:guid}")]
        [ProducesResponseType(typeof(ActivityLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid activityLogId, [FromBody] PatchActivityLogDto dto)
        {
            var userId = User.GetUserId();

            try
            {
                var updated = await _service.UpdateAsync(petId, activityLogId, userId, dto);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(Error(ex.Message));
            }
        }

        [HttpDelete("{activityLogId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid activityLogId)
        {
            var userId = User.GetUserId();

            try
            {
                var deleted = await _service.DeleteAsync(petId, activityLogId, userId);
                if (!deleted) return NotFound(Error("Activity log not found"));
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message));
            }
        }

        private static ApiErrorResponse Error(string message) =>
            ApiErrorResponse.FromMessage(message);
    }
}
