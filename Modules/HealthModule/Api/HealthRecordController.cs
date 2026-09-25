using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.HealthModule.Domain;
using smart_pet_care_api.Modules.HealthModule.DTOs.Requests;
using smart_pet_care_api.Modules.HealthModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets/{petId:guid}/health-records")]
    public class HealthRecordController : ControllerBase
    {
        private readonly IHealthRecordService _service;

        public HealthRecordController(IHealthRecordService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<HealthRecordResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll(
            Guid petId,
            [FromQuery] HealthRecordType? type,
            [FromQuery] SymptomType? symptom,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var userId = User.GetUserId();
            var records = await _service.GetByPetIdAsync(petId, userId, type, symptom, from, to);
            return Ok(records);        }

        [HttpGet("{recordId:guid}")]
        [ProducesResponseType(typeof(HealthRecordResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid petId, Guid recordId)
        {
            var userId = User.GetUserId();
            var record = await _service.GetByIdAsync(petId, recordId, userId);
            if (record is null) return NotFound();
            return Ok(record);        }

        [HttpPost]
        [ProducesResponseType(typeof(HealthRecordResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(Guid petId, [FromBody] CreateHealthRecordDto dto)
        {
            var userId = User.GetUserId();
            var created = await _service.CreateAsync(petId, userId, dto);
            return CreatedAtAction(nameof(GetById), new { petId, recordId = created.Id }, created);        }

        [HttpPatch("{recordId:guid}")]
        [ProducesResponseType(typeof(HealthRecordResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid petId, Guid recordId, [FromBody] PatchHealthRecordDto dto)
        {
            var userId = User.GetUserId();
            var updated = await _service.UpdateAsync(petId, recordId, userId, dto);
            return Ok(updated);        }

        [HttpDelete("{recordId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid petId, Guid recordId)
        {
            var userId = User.GetUserId();
            var deleted = await _service.DeleteAsync(petId, recordId, userId);
            if (!deleted) return NotFound();
            return NoContent();        }
    }
}


