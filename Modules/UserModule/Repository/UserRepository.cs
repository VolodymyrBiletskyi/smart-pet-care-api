using smart_pet_care_api.Common.Api;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.UserModule.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _dbContext;

        public UserRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UserCredentials?> GetCredentialsByEmailAsync(string email)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Email == email)
                .Select(u => new UserCredentials(u.Id, u.Email, u.PasswordHash, u.EmailConfirmed))
                .FirstOrDefaultAsync();
        }

        public async Task<UserCredentials?> GetCredentialsByIdAsync(Guid id)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new UserCredentials(u.Id, u.Email, u.PasswordHash, u.EmailConfirmed))
                .FirstOrDefaultAsync();
        }

        public async Task<UserProfileRow?> GetProfileByIdAsync(Guid id)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new UserProfileRow(
                    u.Id,
                    u.Email,
                    u.DisplayName,
                    u.PhoneNumber,
                    u.AvatarUrl,
                    u.AvatarData != null,
                    u.TermsAccepted,
                    u.CreatedAt,
                    u.UpdatedAt))
                .FirstOrDefaultAsync();
        }

        public async Task<UserAvatar?> GetAvatarByIdAsync(Guid id)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new UserAvatar(u.AvatarData, u.AvatarContentType))
                .FirstOrDefaultAsync();
        }

        public async Task<User?> GetTrackedByEmailAsync(string email)
        {
            return await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        /// <summary>
        /// The whole row, tracked, for the paths that then write to it. Reads should take a
        /// projection above instead — this one carries <c>AvatarData</c> with it.
        /// </summary>
        public Task<User?> GetTrackedByIdAsync(Guid id)
        {
            return _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User> AddAsync(User entity)
        {
            await _dbContext.Users.AddAsync(entity);
            return entity;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
        public async Task DeleteAsync(Guid id)
        {
            var userModel = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == id);

            if (userModel == null)
                throw new NotFoundException(ErrorCodes.User.NotFound, "User not found");

            _dbContext.Users.Remove(userModel);

        }

        public async Task<bool> ExistsAsync(Guid userId)
        {
            return await _dbContext.Users.AnyAsync(u => u.Id == userId);
        }
        public async Task<ExternalLogin?> GetExternalLoginAsync(AuthProvider provider, string providerUserId)
        {
            return await _dbContext.ExternalLogins
                .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderUserId == providerUserId);
        }

        public async Task AddExternalLoginAsync(ExternalLogin login)
        {
            _dbContext.ExternalLogins.Add(login);
            await _dbContext.SaveChangesAsync();
        }
    }
}
