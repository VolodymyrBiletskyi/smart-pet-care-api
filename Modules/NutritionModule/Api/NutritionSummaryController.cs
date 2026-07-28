using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.NutritionModule.Domain;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NutritionModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/nutrition-summary")]
    public class NutritionSummaryController : ControllerBase
    {
        private readonly INutritionService _service;

        public NutritionSummaryController(INutritionService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(DailyNutritionSummaryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDailySummary(
            Guid petId,
            [FromQuery] DateOnly? date,
            [FromQuery] int utcOffsetMinutes = 0)
        {
            try
            {
                var userId = User.GetUserId();
                var summary = await _service.GetDailySummaryAsync(petId, userId, date, utcOffsetMinutes);
                return Ok(summary);
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
    }
}
