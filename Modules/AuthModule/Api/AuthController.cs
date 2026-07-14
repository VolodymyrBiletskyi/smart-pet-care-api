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
            try
            {
                await _authService.RegisterAsync(request);
                return StatusCode(StatusCodes.Status201Created,
                    new { message = "Confirmation code sent, check your email" });
            }
            catch (EmailConfirmationException ex)
            {
                return ConfirmationError(ex);
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

        [HttpPost("confirm-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            try
            {
                await _authService.ConfirmEmailAsync(request);
                return Ok(new { message = "Email confirmed, you can now log in" });
            }
            catch (EmailConfirmationException ex)
            {
                return ConfirmationError(ex);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while confirming the email" });
            }
        }

        [HttpPost("resend-confirmation")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request)
        {
            try
            {
                await _authService.ResendConfirmationAsync(request.Email);
                return NoContent();
            }
            catch (EmailConfirmationException ex)
            {
                return ConfirmationError(ex);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while resending the code" });
            }
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(ToResponse(result));
            }
            catch (EmailConfirmationException ex)
            {
                return ConfirmationError(ex);
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

        private ObjectResult ConfirmationError(EmailConfirmationException ex)
        {
            var status = ex.Error switch
            {
                EmailConfirmationError.AlreadyConfirmed => StatusCodes.Status409Conflict,
                EmailConfirmationError.CodeExpired => StatusCodes.Status410Gone,
                EmailConfirmationError.TooManyAttempts => StatusCodes.Status429TooManyRequests,
                EmailConfirmationError.ResendTooSoon => StatusCodes.Status429TooManyRequests,
                EmailConfirmationError.EmailNotConfirmed => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };
            return StatusCode(status, new { code = ex.ErrorCode, message = ex.Message });
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
            if (string.IsNullOrWhiteSpace(authCode))
                return BadRequest(new { message = "Authorization code is required" });

            try
            {
                var result = await _authService.GoogleLoginAsync(authCode);
                return Ok(ToResponse(result));
            }
            catch (InvalidOperationException)
            {
                return Unauthorized(new { message = "Google authentication failed" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "OAuth login failed" });
            }
        }

        [HttpPost("oauth/google/mobile")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GoogleMobileLogin([FromBody] GoogleMobileRequest request)
        {
            try
            {
                var result = await _authService.GoogleMobileLoginAsync(request.IdToken);
                return Ok(ToResponse(result));
            }
            catch (InvalidOperationException)
            {
                return Unauthorized(new { message = "Google authentication failed" });
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