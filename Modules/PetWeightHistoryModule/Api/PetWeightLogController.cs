using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(Guid petId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid authentication token");

            try
            {
                var logs = await _service.GetByPetIdAsync(petId, userId, from, to);
                return Ok(logs);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(PetWeightLogResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreatePetWeightLogDto dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid authentication token");

            try
            {
                var created = await _service.CreateAsync(petId, userId, dto);
                return Created($"/api/pets/{petId}/weight-history", created);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (PetWeightLogConflictException ex)
            {
                return Conflict(ex.Message);
            }
            catch (DbUpdateException ex) when (IsDuplicateWeightLogMeasuredAt(ex))
            {
                return Conflict("A weight log for this pet already exists at the same measurement time.");
            }
        }

        [HttpPatch("{weightLogId:guid}")]
        [ProducesResponseType(typeof(PetWeightLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid weightLogId, [FromBody] PatchPetWeightLogDto dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid authentication token");

            try
            {
                var updated = await _service.UpdateAsync(petId, weightLogId, userId, dto);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (PetWeightLogConflictException ex)
            {
                return Conflict(ex.Message);
            }
            catch (DbUpdateException ex) when (IsDuplicateWeightLogMeasuredAt(ex))
            {
                return Conflict("A weight log for this pet already exists at the same measurement time.");
            }
        }

        [HttpDelete("{weightLogId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid weightLogId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid authentication token");

            try
            {
                var deleted = await _service.DeleteAsync(petId, weightLogId, userId);
                if (!deleted) return NotFound();
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }

        private bool TryGetUserId(out Guid userId) =>
            Guid.TryParse(User.FindFirst("userId")?.Value, out userId);

        private static bool IsDuplicateWeightLogMeasuredAt(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName == "IX_PetWeightLogs_PetId_MeasuredAt";
        }
    }
}
