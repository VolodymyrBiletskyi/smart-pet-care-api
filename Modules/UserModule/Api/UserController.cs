using smart_pet_care_api.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.UserModule.Domain;
using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.UserModule.Api
{
    [ApiController]
    [Authorize]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPatch]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(PatchUserDto patchDto)
        {
            var userId = User.GetUserId();
            var updatedUser = await _userService.UpdateAsync(userId, patchDto);
            return Ok(updatedUser);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (id != User.GetUserId())
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiErrorResponse.FromMessage(
                        "A user can only delete their own account", ErrorCodes.User.DeleteForbidden));

            var deleted = await _userService.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiErrorResponse.FromMessage("User not found", ErrorCodes.User.NotFound));
            return NoContent();
        }


    }
}
