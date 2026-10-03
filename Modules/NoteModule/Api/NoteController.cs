using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.NoteModule.Domain;
using smart_pet_care_api.Modules.NoteModule.DTOs.Requests;
using smart_pet_care_api.Modules.NoteModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NoteModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/notes")]
    public class NoteController : ControllerBase
    {
        private readonly INoteService _service;

        public NoteController(INoteService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<NoteResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(Guid petId)
        {
            var userId = User.GetUserId();
            var notes = await _service.GetByPetIdAsync(petId, userId);
            return Ok(notes);
        }

        [HttpGet("{noteId:guid}")]
        [ProducesResponseType(typeof(NoteResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid noteId)
        {
            var userId = User.GetUserId();
            var note = await _service.GetByIdAsync(petId, noteId, userId);
            if (note is null)
                return NotFound(ApiErrorResponse.FromMessage("Note not found", ErrorCodes.Note.NotFound));
            return Ok(note);
        }

        [HttpPost]
        [ProducesResponseType(typeof(NoteResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateNoteDto dto)
        {
            var userId = User.GetUserId();
            var created = await _service.CreateAsync(petId, userId, dto);
            return CreatedAtAction(nameof(GetById), new { petId, noteId = created.Id }, created);
        }

        [HttpPatch("{noteId:guid}")]
        [ProducesResponseType(typeof(NoteResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid noteId, [FromBody] PatchNoteDto dto)
        {
            var userId = User.GetUserId();
            var updated = await _service.UpdateAsync(petId, noteId, userId, dto);
            return Ok(updated);
        }

        [HttpDelete("{noteId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid noteId)
        {
            var userId = User.GetUserId();
            var deleted = await _service.DeleteAsync(petId, noteId, userId);
            if (!deleted)
                return NotFound(ApiErrorResponse.FromMessage("Note not found", ErrorCodes.Note.NotFound));
            return NoContent();
        }
    }
}
