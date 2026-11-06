using System.Security.Claims;
using smart_pet_care_api.Common.Api;

namespace smart_pet_care_api.Modules.AuthModule.Jwt
{
    /// <summary>Claim names this API puts in its own access tokens.</summary>
    public static class AuthClaims
    {
        public const string UserId = "userId";
        public const string Email = "email";
        public const string TokenIssuedAt = "tokenIssuedAt";
    }

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

        /// <summary>
        /// When the access token was issued, in UTC. Its own claim rather than the
        /// handler's <c>iat</c>: the standard claim is set by the SDK when it feels like
        /// it, and this one is compared against a stored cutoff that decides whether a
        /// signed, unexpired token is still accepted. Null when the claim is absent or
        /// unparseable, which the caller treats as "unknown", not as "fine".
        /// </summary>
        public static DateTime? GetTokenIssuedAt(this ClaimsPrincipal user)
        {
            var value = user.FindFirst(AuthClaims.TokenIssuedAt)?.Value;

            if (!long.TryParse(value, out var unixMilliseconds))
                return null;

            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime;
        }
    }
}