using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using smart_pet_care_api.Modules.AuthModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterRequest request);
        Task ConfirmEmailAsync(ConfirmEmailRequest request);
        Task ResendConfirmationAsync(string email);

        /// <summary>
        /// Mails a reset link, if the address has an account. Returns the same way
        /// whether it does or not — see the implementation for why.
        /// </summary>
        Task ForgotPasswordAsync(string email);

        Task ResetPasswordAsync(ResetPasswordRequest request);

        /// <summary>
        /// Whether a reset link can still be spent. A read: it is what the landing page
        /// branches on, and that page is also what a mail scanner prefetches.
        /// </summary>
        Task<bool> IsPasswordResetTokenUsableAsync(string rawToken);

        Task<AuthTokenPair> LoginAsync(LoginRequest request);
        Task<AuthTokenPair?> RefreshAsync(string rawRefreshToken);
        Task LogoutAsync(Guid userId, string? deviceToken = null);
        Task<AuthTokenPair> GoogleLoginAsync(string code);
        Task<AuthTokenPair> GoogleMobileLoginAsync(string idToken);
    }
}