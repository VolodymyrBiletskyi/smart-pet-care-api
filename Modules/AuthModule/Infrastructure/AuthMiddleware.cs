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
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            await next(context);
        }
    }
}