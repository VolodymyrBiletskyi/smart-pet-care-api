using System.Security.Claims;

namespace smart_pet_care_api.Modules.AuthModule.Jwt
{
    public static class ClaimsPrincipalExtension
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var id = user.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException("Claim not found in JWT");

            return Guid.Parse(id);
        }

        public static string? GetEmail(this ClaimsPrincipal user)
        {
            return user.FindFirst("email")?.Value;
        }
    }
}