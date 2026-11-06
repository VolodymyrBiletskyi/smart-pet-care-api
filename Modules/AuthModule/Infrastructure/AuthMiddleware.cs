using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.UserModule.Repository;

namespace smart_pet_care_api.Modules.AuthModule.Infrastructure
{
    public class AuthMiddleware : IMiddleware
    {
        private readonly IUserRepository _userRepo;
        private readonly IAuthStateCache _authState;

        public AuthMiddleware(IUserRepository userRepo, IAuthStateCache authState)
        {
            _userRepo = userRepo;
            _authState = authState;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await next(context);
                return;
            }

            var userId = context.User.GetUserId();
            var state = await _authState.GetAsync(userId, () => _userRepo.GetAuthStateAsync(userId));

            if (state is null)
            {
                // A valid token for an account that is gone. Its own code, because
                // refreshing cannot fix it: the client has to start a new session.
                await WriteAsync(
                    context,
                    ErrorCodes.Auth.AccountNoLongerExists,
                    "This account no longer exists.");
                return;
            }

            if (IsRevoked(context, state))
            {
                // Signed out, or the password was reset. The signature is still good and
                // the token has not expired, so nothing before this point rejects it.
                await WriteAsync(
                    context,
                    ErrorCodes.Auth.SessionRevoked,
                    "This session has ended. Sign in again.");
                return;
            }

            await next(context);
        }

        /// <summary>
        /// A token issued before the account's cutoff. Fails closed on a token with no
        /// issue time at all: the only ones in circulation are those signed by a build
        /// that predates the claim, and they last fifteen minutes.
        /// </summary>
        private static bool IsRevoked(HttpContext context, UserAuthState state)
        {
            if (state.TokensValidFrom is not { } cutoff)
                return false;

            return context.User.GetTokenIssuedAt() is not { } issuedAt || issuedAt < cutoff;
        }

        private static Task WriteAsync(HttpContext context, string code, string message)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new ApiErrorResponse
            {
                Code = code,
                Message = message,
                TraceId = context.TraceIdentifier
            });
        }
    }
}
