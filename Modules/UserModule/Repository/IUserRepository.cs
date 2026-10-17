using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.UserModule.Repository
{
    public interface IUserRepository
    {
        /// <summary>Sign-in: the password hash and whether the address was confirmed.</summary>
        Task<UserCredentials?> GetCredentialsByEmailAsync(string email);

        /// <summary>The same columns for a refresh, which reissues from the user id.</summary>
        Task<UserCredentials?> GetCredentialsByIdAsync(Guid id);

        /// <summary>Everything the profile response needs, and nothing else.</summary>
        Task<UserProfileRow?> GetProfileByIdAsync(Guid id);

        /// <summary>The avatar bytes, for the route that actually serves them.</summary>
        Task<UserAvatar?> GetAvatarByIdAsync(Guid id);

        /// <summary>Tracked, for write paths only — it loads <c>AvatarData</c> too.</summary>
        Task<User?> GetTrackedByIdAsync(Guid id);
        Task<User?> GetTrackedByEmailAsync(string email);
        Task<User> AddAsync(User entity);
        Task<int> SaveChangesAsync();
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid userId);
        Task<ExternalLogin?> GetExternalLoginAsync(AuthProvider provider, string providerUserId);
        Task AddExternalLoginAsync(ExternalLogin login);

    }
}
