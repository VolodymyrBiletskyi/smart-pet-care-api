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
            var userId = User.GetUserId();
            return Ok(await _service.GetByPetIdAsync(petId, userId));
        }

        [HttpGet("{logId:guid}")]
        [ProducesResponseType(typeof(FeedingLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid logId)
        {
            var userId = User.GetUserId();
            var log = await _service.GetByIdAsync(petId, logId, userId);
            if (log is null)
                return NotFound(Error("Feeding log not found", ErrorCodes.Feeding.LogNotFound));

            return Ok(log);
        }

        [HttpPost]
        [ProducesResponseType(typeof(FeedingLogResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateFeedingLogDto dto)
        {
            var userId = User.GetUserId();
            var created = await _service.CreateAsync(petId, userId, dto);
            return CreatedAtAction(nameof(GetById), new { petId, logId = created.Id }, created);
        }

        [HttpPatch("{logId:guid}")]
        [ProducesResponseType(typeof(FeedingLogResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid logId, [FromBody] PatchFeedingLogDto dto)
        {
            var userId = User.GetUserId();
            return Ok(await _service.UpdateAsync(petId, logId, userId, dto));
        }

        [HttpDelete("{logId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid logId)
        {
            var userId = User.GetUserId();
            var deleted = await _service.DeleteAsync(petId, logId, userId);
            if (!deleted)
                return NotFound(Error("Feeding log not found", ErrorCodes.Feeding.LogNotFound));

            return NoContent();
        }

        private static ApiErrorResponse Error(string message, string code) =>
            ApiErrorResponse.FromMessage(message, code);
    }
}
