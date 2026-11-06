using Microsoft.Extensions.Caching.Memory;
using smart_pet_care_api.Modules.UserModule.Repository;

namespace smart_pet_care_api.Modules.AuthModule.Infrastructure
{
    /// <summary>
    /// The per-user answer <see cref="AuthMiddleware"/> needs on every authenticated
    /// request, held for a few minutes so a valid token does not cost a query.
    /// <para>
    /// It is a service rather than a direct <see cref="IMemoryCache"/> call in the
    /// middleware because the entry now decides whether an access token is still
    /// accepted, and the paths that change that answer — signing out, resetting a
    /// password — have to drop it. Left to expire on its own, a sign-out would take
    /// effect somewhere between immediately and three minutes later.
    /// </para>
    /// </summary>
    public interface IAuthStateCache
    {
        Task<UserAuthState?> GetAsync(Guid userId, Func<Task<UserAuthState?>> load);

        void Invalidate(Guid userId);
    }

    public sealed class AuthStateCache : IAuthStateCache
    {
        private const string KeyPrefix = "user-auth-state:";
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);

        private readonly IMemoryCache _cache;

        public AuthStateCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<UserAuthState?> GetAsync(Guid userId, Func<Task<UserAuthState?>> load)
        {
            var key = Key(userId);

            // A missing account is cached too, as the absent state: a token for a deleted
            // account is refused on every request, and each of those used to be a query.
            if (_cache.TryGetValue(key, out UserAuthState? cached))
                return cached;

            var state = await load();
            _cache.Set(key, state, Ttl);
            return state;
        }

        public void Invalidate(Guid userId) => _cache.Remove(Key(userId));

        private static string Key(Guid userId) => KeyPrefix + userId.ToString("N");
    }
}
