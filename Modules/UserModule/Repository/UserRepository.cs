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

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);
        }

        public Task<User?> GetByIdAsync(Guid id)
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
                throw new KeyNotFoundException("User not found");

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