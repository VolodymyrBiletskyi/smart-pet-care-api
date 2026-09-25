using smart_pet_care_api.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.NutritionModule.Domain;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NutritionModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/nutrition-goal")]
    public class NutritionGoalController : ControllerBase
    {
        private readonly INutritionService _service;

        public NutritionGoalController(INutritionService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(NutritionGoalResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Get(Guid petId)
        {
            var userId = User.GetUserId();
            var goal = await _service.GetGoalAsync(petId, userId);
            if (goal is null)
                return NotFound(ApiErrorResponse.FromMessage("Nutrition goal not found", ErrorCodes.Nutrition.GoalNotFound));
            return Ok(goal);
        }

        [HttpPut]
        [ProducesResponseType(typeof(NutritionGoalResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Upsert(Guid petId, [FromBody] UpsertNutritionGoalDto dto)
        {
            var userId = User.GetUserId();
            var goal = await _service.UpsertGoalAsync(petId, userId, dto);
            return Ok(goal);
        }

        [HttpPatch]
        [ProducesResponseType(typeof(NutritionGoalResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Patch(Guid petId, [FromBody] PatchNutritionGoalDto dto)
        {
            var userId = User.GetUserId();
            var goal = await _service.PatchGoalAsync(petId, userId, dto);
            return Ok(goal);
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId)
        {
            var userId = User.GetUserId();
            var deleted = await _service.DeleteGoalAsync(petId, userId);
            if (!deleted)
                return NotFound(ApiErrorResponse.FromMessage("Nutrition goal not found", ErrorCodes.Nutrition.GoalNotFound));
            return NoContent();
        }
    }
}

