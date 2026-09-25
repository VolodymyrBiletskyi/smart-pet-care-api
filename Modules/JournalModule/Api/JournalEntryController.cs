using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.JournalModule.Domain;
using smart_pet_care_api.Modules.JournalModule.DTOs.Requests;
using smart_pet_care_api.Modules.JournalModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.JournalModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/journal")]
    public class JournalEntryController : ControllerBase
    {
        private readonly IJournalEntryService _service;

        public JournalEntryController(IJournalEntryService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<JournalEntryResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(
            Guid petId,
            [FromQuery] JournalEntryType? type,
            [FromQuery] JournalEntrySeverity? severity,
            [FromQuery] SymptomType? symptom,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var userId = User.GetUserId();
            var entries = await _service.GetByPetIdAsync(petId, userId, type, severity, symptom, from, to);
            return Ok(entries);        }

        [HttpGet("{entryId:guid}")]
        [ProducesResponseType(typeof(JournalEntryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid entryId)
        {
            var userId = User.GetUserId();
            var entry = await _service.GetByIdAsync(petId, entryId, userId);
            if (entry is null) return NotFound();
            return Ok(entry);        }

        [HttpPost]
        [ProducesResponseType(typeof(JournalEntryResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateJournalEntryDto dto)
        {
            var userId = User.GetUserId();
            var created = await _service.CreateAsync(petId, userId, dto);
            return CreatedAtAction(nameof(GetById), new { petId, entryId = created.Id }, created);        }

        [HttpPatch("{entryId:guid}")]
        [ProducesResponseType(typeof(JournalEntryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid entryId, [FromBody] PatchJournalEntryDto dto)
        {
            var userId = User.GetUserId();
            var updated = await _service.UpdateAsync(petId, entryId, userId, dto);
            return Ok(updated);        }

        [HttpDelete("{entryId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid entryId)
        {
            var userId = User.GetUserId();
            var deleted = await _service.DeleteAsync(petId, entryId, userId);
            if (!deleted) return NotFound();
            return NoContent();        }
    }
}


