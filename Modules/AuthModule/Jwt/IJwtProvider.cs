using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.AuthModule.Jwt
{
    public interface IJwtProvider
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
        string HashToken(string token);

        int AccessTokenMinutes { get; }
    }
}