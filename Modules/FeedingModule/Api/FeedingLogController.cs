using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.FeedingModule.Domain;
using smart_pet_care_api.Modules.FeedingModule.DTOs.Requests;
using smart_pet_care_api.Modules.FeedingModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.FeedingModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/feeding-logs")]
    public class FeedingLogController : ControllerBase
    {
        private readonly IFeedingLogService _service;

        public FeedingLogController(IFeedingLogService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<FeedingLogResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(Guid petId)
        {
            try
            {
                var userId = User.GetUserId();
                var logs = await _service.GetByPetIdAsync(petId, userId);
                return Ok(logs);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, NotFoundErrorCode(ex.Message)));
            }
        }

        [HttpGet("{logId:guid}")]
        [ProducesResponseType(typeof(FeedingLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid logId)
        {
            try
            {
                var userId = User.GetUserId();
                var log = await _service.GetByIdAsync(petId, logId, userId);
                if (log is null) return NotFound(Error("Feeding log not found", "feeding_log_not_found"));
                return Ok(log);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, NotFoundErrorCode(ex.Message)));
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(FeedingLogResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateFeedingLogDto dto)
        {
            try
            {
                var userId = User.GetUserId();
                var created = await _service.CreateAsync(petId, userId, dto);
                return CreatedAtAction(nameof(GetById), new { petId, logId = created.Id }, created);
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

        [HttpPatch("{logId:guid}")]
        [ProducesResponseType(typeof(FeedingLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid logId, [FromBody] PatchFeedingLogDto dto)
        {
            try
            {
                var userId = User.GetUserId();
                var updated = await _service.UpdateAsync(petId, logId, userId, dto);
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
        }

        [HttpDelete("{logId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid logId)
        {
            try
            {
                var userId = User.GetUserId();
                var deleted = await _service.DeleteAsync(petId, logId, userId);
                if (!deleted) return NotFound(Error("Feeding log not found", "feeding_log_not_found"));
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(Error(ex.Message, NotFoundErrorCode(ex.Message)));
            }
        }

        private static ApiErrorResponse Error(string message, string code) =>
            ApiErrorResponse.FromMessage(message, code);

        private static string NotFoundErrorCode(string message) => message switch
        {
            "Pet not found" => "pet_not_found",
            "Reminder not found" => "reminder_not_found",
            _ => "feeding_log_not_found"
        };

        private static string ValidationErrorCode(string message) => message switch
        {
            "PortionUnit is required when PortionAmount is specified" => "feeding_portion_unit_required",
            "At least one field must be provided" => "feeding_update_empty",
            "FedAt is required" => "feeding_time_required",
            "FedAt cannot be more than 10 minutes in the future" => "feeding_time_too_far_in_future",
            "PortionAmount cannot be negative" => "feeding_portion_amount_negative",
            "ApproxCalories cannot be negative" => "feeding_calories_negative",
            "FoodType is invalid" => "feeding_food_type_invalid",
            "PortionUnit is invalid" => "feeding_portion_unit_invalid",
            _ => "feeding_validation_failed"
        };
    }
}
