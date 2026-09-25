using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(Guid petId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(Error("Authentication token is invalid", "authentication_token_invalid"));

            try
            {
                var logs = await _service.GetByPetIdAsync(petId, userId, from, to);
                return Ok(logs);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, NotFoundErrorCode(ex.Message)));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(Error(ex.Message, ValidationErrorCode(ex.Message)));
            }
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
                return Unauthorized(Error("Authentication token is invalid", "authentication_token_invalid"));

            try
            {
                var created = await _service.CreateAsync(petId, userId, dto);
                return Created($"/api/pets/{petId}/weight-history", created);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, NotFoundErrorCode(ex.Message)));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(Error(ex.Message, ValidationErrorCode(ex.Message)));
            }
            catch (PetWeightLogConflictException ex)
            {
                return Conflict(Error(ex.Message, "weight_log_measurement_time_conflict"));
            }
            catch (DbUpdateException ex) when (IsDuplicateWeightLogMeasuredAt(ex))
            {
                return Conflict(Error(
                    "A weight log for this pet already exists at the same measurement time",
                    "weight_log_measurement_time_conflict"));
            }
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
                return Unauthorized(Error("Authentication token is invalid", "authentication_token_invalid"));

            try
            {
                var updated = await _service.UpdateAsync(petId, weightLogId, userId, dto);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, NotFoundErrorCode(ex.Message)));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(Error(ex.Message, ValidationErrorCode(ex.Message)));
            }
            catch (PetWeightLogConflictException ex)
            {
                return Conflict(Error(ex.Message, "weight_log_measurement_time_conflict"));
            }
            catch (DbUpdateException ex) when (IsDuplicateWeightLogMeasuredAt(ex))
            {
                return Conflict(Error(
                    "A weight log for this pet already exists at the same measurement time",
                    "weight_log_measurement_time_conflict"));
            }
        }

        [HttpDelete("{weightLogId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid weightLogId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(Error("Authentication token is invalid", "authentication_token_invalid"));

            try
            {
                var deleted = await _service.DeleteAsync(petId, weightLogId, userId);
                if (!deleted) return NotFound(Error("Weight log not found", "weight_log_not_found"));
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, NotFoundErrorCode(ex.Message)));
            }
        }

        private bool TryGetUserId(out Guid userId) =>
            Guid.TryParse(User.FindFirst("userId")?.Value, out userId);

        private static ApiErrorResponse Error(string message, string code) =>
            ApiErrorResponse.FromMessage(message, code);

        private static string NotFoundErrorCode(string message) => message switch
        {
            "Pet not found" => "pet_not_found",
            "Reminder not found" => "reminder_not_found",
            _ => "weight_log_not_found"
        };

        private static string ValidationErrorCode(string message) => message switch
        {
            "At least one field must be provided" => "weight_log_update_empty",
            "WeightKg must be greater than 0" => "weight_log_weight_not_positive",
            "WeightKg cannot be greater than 230" => "weight_log_weight_too_large",
            "MeasuredAt is required" => "weight_log_measurement_time_required",
            "MeasuredAt cannot be more than 10 minutes in the future" => "weight_log_measurement_time_too_far_in_future",
            "From cannot be later than To" => "weight_log_date_range_invalid",
            "Notes cannot be whitespace only" => "weight_log_notes_empty",
            _ => "weight_log_validation_failed"
        };

        private static bool IsDuplicateWeightLogMeasuredAt(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName == "IX_PetWeightLogs_PetId_MeasuredAt";
        }
    }
}
