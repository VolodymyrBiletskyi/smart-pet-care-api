using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.AuthModule.Repository
{
    public class AuthRepository : IAuthRepository
    {
        private readonly AppDbContext _dbContext;
        public AuthRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(RefreshToken token)
        {
            await _dbContext.RefreshTokens.AddAsync(token);
        }

        public async Task<List<RefreshToken>> GetActiveByUserAsync(Guid userId)
        {
            var now = DateTime.UtcNow;

            return await _dbContext.RefreshTokens
                .Where(t =>
                    t.UserId == userId &&
                    t.RevokedAt == null &&
                    t.ExpiresAt > now)
                .OrderByDescending(t => t.ExpiresAt)
                .Take(10)
                .ToListAsync();
        }

        public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            return await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}