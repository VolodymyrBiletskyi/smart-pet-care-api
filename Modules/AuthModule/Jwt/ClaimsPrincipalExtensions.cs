using System.Security.Claims;
using smart_pet_care_api.Common.Api;

namespace smart_pet_care_api.Modules.AuthModule.Jwt
{
    public static class ClaimsPrincipalExtension
    {
        /// <summary>
        /// A token that carries no usable user id is a bad credential, not a
        /// server fault: every caller of this used to get a 500 for it, except
        /// the one controller that happened to parse the claim by hand.
        /// </summary>
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var id = user.FindFirst("userId")?.Value;

            if (!Guid.TryParse(id, out var userId))
                throw new UnauthorizedException(
                    ErrorCodes.AuthenticationTokenInvalid, "Authentication token is invalid");

            return userId;
        }

        public static string? GetEmail(this ClaimsPrincipal user)
        {
            return user.FindFirst("email")?.Value;
        }
    }
}