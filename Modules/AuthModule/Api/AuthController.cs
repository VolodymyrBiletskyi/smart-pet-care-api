using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using smart_pet_care_api.Modules.AuthModule.DTOs.Responses;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.AuthModule.OAuth;

namespace smart_pet_care_api.Modules.AuthModule.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IGoogleOAuth _googleOAuth;

        public AuthController(IAuthService authService, IGoogleOAuth googleOAuth)
        {
            _authService = authService;
            _googleOAuth = googleOAuth;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                return StatusCode(StatusCodes.Status201Created, ToResponse(result));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while registering" });
            }
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(ToResponse(result));
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while logging in" });
            }
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var result = await _authService.RefreshAsync(request.RefreshToken);
                if (result is null)
                    return Unauthorized(new { message = "Invalid or expired refresh token" });

                return Ok(ToResponse(result));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while refreshing the token" });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.GetUserId();
                await _authService.LogoutAsync(userId);
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while logging out" });
            }
        }
        [HttpGet("oauth/google")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GoogleLogin()
        {
            var url = _googleOAuth.GetAuthorizationUrl();
            return Redirect(url);
        }

        [HttpGet("oauth/google/callback")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GoogleCallback([FromQuery(Name = "code")] string? authCode)
        {
            try
            {
                var result = await _authService.GoogleLoginAsync(authCode);
                return Ok(ToResponse(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("oauth/google/mobile")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GoogleMobileLogin([FromBody] GoogleMobileRequest request)
        {
            try
            {
                var result = await _authService.GoogleMobileLoginAsync(request.IdToken);
                return Ok(ToResponse(result));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "OAuth login failed" });
            }
        }

        private static AuthResponse ToResponse(AuthTokenPair pair)
        {
            pair.Auth.RefreshToken = pair.RefreshToken;
            return pair.Auth;
        }
    }
}