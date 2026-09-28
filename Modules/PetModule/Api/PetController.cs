using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Infrastructure.Cloudinary;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.PetModule.Domain;
using smart_pet_care_api.Modules.PetModule.DTOs;

namespace smart_pet_care_api.Modules.PetModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/pets")]
    public class PetController : ControllerBase
    {
        private readonly IPetService _petService;

        public PetController(IPetService petService)
        {
            _petService = petService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<PetResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll()
        {
            var userId = User.GetUserId();
            var pets = await _petService.GetByUserIdAsync(userId);
            return Ok(pets);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PetResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = User.GetUserId();
            var pet = await _petService.GetByIdAsync(id, userId);
            if (pet == null) return NotFound(Error("Pet not found.", "pet_not_found"));
            return Ok(pet);
        }

        [HttpPost]
        [ProducesResponseType(typeof(PetResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(CreatePetDto dto)
        {
            var userId = User.GetUserId();
            var createdPet = await _petService.CreateAsync(dto, userId);
            return CreatedAtAction(nameof(GetById), new { id = createdPet.Id }, createdPet);
        }

        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(PetResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid id, UpdatePetDto dto)
        {
            var userId = User.GetUserId();
            return Ok(await _petService.UpdateAsync(id, userId, dto));
        }

        [HttpPatch("{id}/photo")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(PetResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> UpdatePhoto(Guid id, [FromForm] UploadPetPhotoDto dto)
        {
            var userId = User.GetUserId();
            var photo = dto.Photo
                ?? Request.Form.Files.GetFile("photo")
                ?? Request.Form.Files.GetFile("Photo")
                ?? Request.Form.Files.FirstOrDefault();

            return Ok(await _petService.UpdatePhotoAsync(id, userId, photo));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = User.GetUserId();
            var deleted = await _petService.DeleteAsync(id, userId);
            if (!deleted) return NotFound(Error("Pet not found.", "pet_not_found"));
            return NoContent();
        }

        private static ApiErrorResponse Error(string message, string code) =>
            ApiErrorResponse.FromMessage(message, code);
    }
}
