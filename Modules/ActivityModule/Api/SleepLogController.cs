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

            try
            {
                var logs = await _service.GetByPetIdAsync(petId, userId, from, to);
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

        [HttpGet("{sleepLogId:guid}")]
        [ProducesResponseType(typeof(SleepLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid sleepLogId)
        {
            var userId = User.GetUserId();

            try
            {
                var log = await _service.GetByIdAsync(petId, sleepLogId, userId);
                if (log is null) return NotFound(Error("Sleep log not found"));
                return Ok(log);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message));
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(SleepLogResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateSleepLogDto dto)
        {
            var userId = User.GetUserId();

            try
            {
                var created = await _service.CreateAsync(petId, userId, dto);
                return CreatedAtAction(nameof(GetById), new { petId, sleepLogId = created.Id }, created);
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

        [HttpDelete("{sleepLogId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid sleepLogId)
        {
            var userId = User.GetUserId();

            try
            {
                var deleted = await _service.DeleteAsync(petId, sleepLogId, userId);
                if (!deleted) return NotFound(Error("Sleep log not found"));
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
