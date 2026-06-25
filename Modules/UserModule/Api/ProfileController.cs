using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.UserModule.Domain;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.UserModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IUserService _userService;

        public ProfileController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.GetUserId();
            var user = await _userService.GetByIdAsync(userId);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpPost("avatar")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            if (file.Length > 1 * 1024 * 1024)
                return BadRequest("Photo must be 1 MB or less");

            var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowed.Contains(file.ContentType))
                return BadRequest("Only JPEG, PNG, and WebP images are allowed");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var userId = User.GetUserId();
            var user = await _userService.SaveAvatarAsync(userId, ms.ToArray(), file.ContentType);
            return Ok(user);
        }

        [AllowAnonymous]
        [HttpGet("avatar/{userId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAvatar(Guid userId)
        {
            var avatar = await _userService.GetAvatarAsync(userId);
            if (avatar == null) return NotFound();
            return File(avatar.Value.Data, avatar.Value.ContentType);
        }
    }
}
