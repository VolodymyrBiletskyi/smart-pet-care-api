using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using smart_pet_care_api.Modules.AuthModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public interface IAuthService
    {
        Task<AuthTokenPair> RegisterAsync(RegisterRequest request);
        Task<AuthTokenPair> LoginAsync(LoginRequest request);
        Task<AuthTokenPair?> RefreshAsync(string rawRefreshToken);
        Task LogoutAsync(Guid userId);
        Task<AuthTokenPair> GoogleLoginAsync(string code);
        Task<AuthTokenPair> GoogleMobileLoginAsync(string idToken);
    }
}