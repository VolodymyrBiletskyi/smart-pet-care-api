using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;

namespace smart_pet_care_api.Modules.ReminderModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/reminders")]
    public class ReminderController : ControllerBase
    {
        private readonly IReminderService _service;

        public ReminderController(IReminderService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] Guid? petId)
        {
            var userId = User.GetUserId();

            var reminders = petId.HasValue
                ? await _service.GetByPetIdAsync(petId.Value, userId)
                : await _service.GetByUserIdAsync(userId);

            return Ok(reminders);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = User.GetUserId();
            var reminder = await _service.GetByIdAsync(id, userId);
            if (reminder == null) return NotFound();
            return Ok(reminder);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReminderDto dto)
        {
            try
            {
                var userId = User.GetUserId();
                var created = await _service.CreateAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PatchReminderDto dto)
        {
            try
            {
                var userId = User.GetUserId();
                var updated = await _service.UpdateAsync(id, dto, userId);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var userId = User.GetUserId();
                await _service.DeleteAsync(id, userId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("{id:guid}/runs")]
        public async Task<IActionResult> GetRuns(Guid id)
        {
            var userId = User.GetUserId();
            var runs = await _service.GetRunsAsync(id, userId);
            return Ok(runs);
        }

        [HttpPost("runs/{runId:guid}/acknowledge")]
        public async Task<IActionResult> AcknowledgeRun(Guid runId)
        {
            try
            {
                var userId = User.GetUserId();
                var run = await _service.AcknowledgeRunAsync(runId, userId);
                return Ok(run);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
