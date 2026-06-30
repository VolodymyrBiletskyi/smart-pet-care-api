using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.NotificationModule.Domain;
using smart_pet_care_api.Modules.NotificationModule.DTOs.Requests;

namespace smart_pet_care_api.Modules.NotificationModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/notifications")]
    public class NotificationController : ControllerBase
    {
        private readonly IDeviceTokenService _deviceTokenService;

        public NotificationController(IDeviceTokenService deviceTokenService)
        {
            _deviceTokenService = deviceTokenService;
        }

        [HttpPost("device-token")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceTokenDto dto)
        {
            try
            {
                var userId = User.GetUserId();
                await _deviceTokenService.RegisterAsync(userId, dto);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("device-token/{token}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Unregister(string token)
        {
            var userId = User.GetUserId();
            var removed = await _deviceTokenService.UnregisterAsync(userId, token);
            return removed ? NoContent() : NotFound();
        }
    }
}
