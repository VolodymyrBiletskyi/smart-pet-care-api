using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.AuthModule.Mapper
{
    public static class AuthMapper
    {
        public static AuthResponse ToAuthResponse(User user, string accessToken, DateTime expiresAt)
        {
            return new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                AccessToken = accessToken,
                ExpiresAtUtc = expiresAt
            };
        }

        public static AuthTokenPair ToAuthTokenPair(AuthResponse auth, string rawRefresh)
        {
            return new AuthTokenPair
            {
                Auth = auth,
                RefreshToken = rawRefresh
            };
        }

        public static RefreshToken ToRefreshTokenEntity(Guid userId, string hash, int expiryDays = 7)
        {
            return new RefreshToken
            {
                UserId = userId,
                TokenHash = hash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
            };
        }
    }
}