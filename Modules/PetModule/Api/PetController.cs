using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = User.GetUserId();
            var pet = await _petService.GetByIdAsync(id, userId);
            if (pet == null) return NotFound();
            return Ok(pet);
        }

        [HttpPost]
        [ProducesResponseType(typeof(PetResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(CreatePetDto dto)
        {
            try
            {
                var userId = User.GetUserId();
                var createdPet = await _petService.CreateAsync(dto, userId);
                return CreatedAtAction(nameof(GetById), new { id = createdPet.Id }, createdPet);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(PetResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid id, UpdatePetDto dto)
        {
            try
            {
                var userId = User.GetUserId();
                var updatedPet = await _petService.UpdateAsync(id, userId, dto);
                return Ok(updatedPet);
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

        [HttpPatch("{id}/photo")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(PetResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> UpdatePhoto(Guid id, [FromForm] UploadPetPhotoDto dto)
        {
            try
            {
                var userId = User.GetUserId();
                var photo = dto.Photo
                    ?? Request.Form.Files.GetFile("photo")
                    ?? Request.Form.Files.GetFile("Photo")
                    ?? Request.Form.Files.FirstOrDefault();

                var updatedPet = await _petService.UpdatePhotoAsync(id, userId, photo);
                return Ok(updatedPet);
            }
            catch (CloudinaryUploadException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
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

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = User.GetUserId();
            var deleted = await _petService.DeleteAsync(id, userId);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
