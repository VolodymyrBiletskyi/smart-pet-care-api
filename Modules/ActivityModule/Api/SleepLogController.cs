using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using smart_pet_care_api.Modules.AuthModule.Jwt;

namespace smart_pet_care_api.Modules.ActivityModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/sleep-logs")]
    public class SleepLogController : ControllerBase
    {
        private readonly ISleepLogService _service;

        public SleepLogController(ISleepLogService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SleepLogResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(
            Guid petId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var userId = User.GetUserId();

            var logs = await _service.GetByPetIdAsync(petId, userId, from, to);
            return Ok(logs);        }

        [HttpGet("{sleepLogId:guid}")]
        [ProducesResponseType(typeof(SleepLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid sleepLogId)
        {
            var userId = User.GetUserId();

            var log = await _service.GetByIdAsync(petId, sleepLogId, userId);
            if (log is null) return NotFound(Error("Sleep log not found", ErrorCodes.Sleep.LogNotFound));
            return Ok(log);        }

        [HttpPost]
        [ProducesResponseType(typeof(SleepLogResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateSleepLogDto dto)
        {
            var userId = User.GetUserId();

            var created = await _service.CreateAsync(petId, userId, dto);
            return CreatedAtAction(nameof(GetById), new { petId, sleepLogId = created.Id }, created);        }

        [HttpPatch("{sleepLogId:guid}")]
        [ProducesResponseType(typeof(SleepLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid sleepLogId, [FromBody] PatchSleepLogDto dto)
        {
            var userId = User.GetUserId();

            var updated = await _service.UpdateAsync(petId, sleepLogId, userId, dto);
            return Ok(updated);        }

        [HttpDelete("{sleepLogId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid sleepLogId)
        {
            var userId = User.GetUserId();

            var deleted = await _service.DeleteAsync(petId, sleepLogId, userId);
            if (!deleted) return NotFound(Error("Sleep log not found", ErrorCodes.Sleep.LogNotFound));
            return NoContent();        }

        private static ApiErrorResponse Error(string message, string code) =>
            ApiErrorResponse.FromMessage(message, code);
    }
}


