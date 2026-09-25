using smart_pet_care_api.Common.Api;
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
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            await _authService.RegisterAsync(request);
            return StatusCode(StatusCodes.Status201Created,
                new { message = "Confirmation code sent, check your email" });
        }

        [HttpPost("confirm-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            await _authService.ConfirmEmailAsync(request);
            return Ok(new { message = "Email confirmed, you can now log in" });
        }

        [HttpPost("resend-confirmation")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request)
        {
            await _authService.ResendConfirmationAsync(request.Email);
            return NoContent();
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ToResponse(result));
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshAsync(request.RefreshToken);
            if (result is null)
                return Unauthorized(ApiErrorResponse.FromMessage(
                    "Invalid or expired refresh token", ErrorCodes.Auth.RefreshTokenInvalid));

            return Ok(ToResponse(result));
        }

        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            var userId = User.GetUserId();
            await _authService.LogoutAsync(userId);
            return NoContent();
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
            if (string.IsNullOrWhiteSpace(authCode))
                return BadRequest(ApiErrorResponse.FromMessage(
                    "Authorization code is required", ErrorCodes.Auth.OAuthCodeRequired));

            var result = await _authService.GoogleLoginAsync(authCode);
            return Ok(ToResponse(result));
        }

        [HttpPost("oauth/google/mobile")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GoogleMobileLogin([FromBody] GoogleMobileRequest request)
        {
            var result = await _authService.GoogleMobileLoginAsync(request.IdToken);
            return Ok(ToResponse(result));
        }

        private static AuthResponse ToResponse(AuthTokenPair pair)
        {
            pair.Auth.RefreshToken = pair.RefreshToken;
            return pair.Auth;
        }
    }
}
