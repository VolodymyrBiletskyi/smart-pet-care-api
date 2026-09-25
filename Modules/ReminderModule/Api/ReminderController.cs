using smart_pet_care_api.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/reminders")]
    public class ReminderController : ControllerBase
    {
        private readonly IReminderService _service;
        private readonly IReminderCompletionService _completion;

        public ReminderController(IReminderService service, IReminderCompletionService completion)
        {
            _service = service;
            _completion = completion;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ReminderResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Get([FromQuery] Guid? petId)
        {
            var userId = User.GetUserId();

            var reminders = petId.HasValue
                ? await _service.GetByPetIdAsync(petId.Value, userId)
                : await _service.GetByUserIdAsync(userId);

            return Ok(reminders);
        }

        [HttpGet("pet/{petId:guid}")]
        [ProducesResponseType(typeof(IEnumerable<ReminderResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetByPetId(Guid petId)
        {
            var userId = User.GetUserId();
            var reminders = await _service.GetByPetIdAsync(petId, userId);
            return Ok(reminders);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ReminderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = User.GetUserId();
            var reminder = await _service.GetByIdAsync(id, userId);
            if (reminder is null)
                return NotFound(ApiErrorResponse.FromMessage(
                    "Reminder not found", ErrorCodes.ReminderNotFound));
            return Ok(reminder);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ReminderResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] CreateReminderDto dto)
        {
            var userId = User.GetUserId();
            var created = await _service.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPatch("{id:guid}")]
        [ProducesResponseType(typeof(ReminderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid id, [FromBody] PatchReminderDto dto)
        {
            var userId = User.GetUserId();
            var updated = await _service.UpdateAsync(id, dto, userId);
            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = User.GetUserId();
            await _service.DeleteAsync(id, userId);
            return NoContent();
        }

        [HttpGet("{id:guid}/runs")]
        [ProducesResponseType(typeof(IEnumerable<ReminderRunResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetRuns(Guid id)
        {
            var userId = User.GetUserId();
            var runs = await _service.GetRunsAsync(id, userId);
            return Ok(runs);
        }

        /// <summary>
        /// The Done button. Takes the date in the body so the user can correct it in the log
        /// window, or confirm before the notification has even fired.
        /// </summary>
        [HttpPost("{id:guid}/complete")]
        [ProducesResponseType(typeof(ReminderCompletionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteReminderDto? dto)
        {
            var userId = User.GetUserId();
            var result = await _completion.CompleteAsync(id, userId, dto ?? new CompleteReminderDto());
            return Ok(result);
        }

        /// <summary>
        /// Occurrences over a window. Future ones are projected from the repeat rules and are
        /// not stored, so they carry no run id until something actually happens to them.
        /// </summary>
        [HttpGet("occurrences")]
        [ProducesResponseType(typeof(IEnumerable<ReminderOccurrenceDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetOccurrences(
            [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] Guid? petId)
        {
            var userId = User.GetUserId();
            var occurrences = await _service.GetOccurrencesAsync(userId, petId, from, to);
            return Ok(occurrences);
        }

        /// <summary>Execution history across every rule of one pet, for a period.</summary>
        [HttpGet("runs")]
        [ProducesResponseType(typeof(IEnumerable<ReminderRunHistoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetRunHistory(
            [FromQuery] Guid petId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] ReminderType? type)
        {
            var userId = User.GetUserId();
            var history = await _service.GetRunHistoryAsync(petId, userId, from, to, type);
            return Ok(history);
        }

        [HttpPost("runs/{runId:guid}/acknowledge")]
        [ProducesResponseType(typeof(ReminderRunResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]

        public async Task<IActionResult> AcknowledgeRun(Guid runId)
        {
            var userId = User.GetUserId();
            var run = await _service.AcknowledgeRunAsync(runId, userId);
            return Ok(run);
        }
    }
}

