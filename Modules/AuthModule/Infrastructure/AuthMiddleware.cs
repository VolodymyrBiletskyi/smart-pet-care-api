using smart_pet_care_api.Common.Api;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.AuthModule.Repository;
using smart_pet_care_api.Modules.UserModule.Repository;

namespace smart_pet_care_api.Modules.AuthModule.Infrastructure
{
    public class AuthMiddleware : IMiddleware
    {
        private const string CacheKeyPrefix = "user-active:";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);

        private readonly IAuthRepository _authRepo;
        private readonly IUserRepository _userRepo;
        private readonly IMemoryCache _cache;

        public AuthMiddleware(IAuthRepository authRepo, IUserRepository userRepo, IMemoryCache cache)
        {
            _authRepo = authRepo;
            _userRepo = userRepo;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await next(context);
                return;
            }

            var userId = context.User.GetUserId();
            var cacheKey = CacheKeyPrefix + userId.ToString("N");

            if (!_cache.TryGetValue(cacheKey, out bool isValid))
            {
                isValid = await _userRepo.ExistsAsync(userId);
                _cache.Set(cacheKey, isValid, CacheTtl);
            }

            if (!isValid)
            {
                // A valid token for an account that is gone. Its own code, because
                // refreshing cannot fix it: the client has to start a new session.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ApiErrorResponse
                {
                    Code = ErrorCodes.Auth.AccountNoLongerExists,
                    Message = "This account no longer exists.",
                    TraceId = context.TraceIdentifier
                });
                return;
            }

            await next(context);
        }
    }
}