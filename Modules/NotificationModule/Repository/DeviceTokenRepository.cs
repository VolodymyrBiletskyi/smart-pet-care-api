using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NotificationModule.Repository
{
    public class DeviceTokenRepository : IDeviceTokenRepository
    {
        private readonly AppDbContext _dbContext;

        public DeviceTokenRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<DeviceToken?> GetByUserAndTokenAsync(Guid userId, string token)
        {
            return await _dbContext.DeviceTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token);
        }

        public async Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId)
        {
            return await _dbContext.DeviceTokens
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .ToListAsync();
        }

        public async Task AddAsync(DeviceToken token)
        {
            await _dbContext.DeviceTokens.AddAsync(token);
        }

        public async Task<bool> RemoveByUserAndTokenAsync(Guid userId, string token)
        {
            var existing = await _dbContext.DeviceTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token);

            if (existing is null) return false;

            _dbContext.DeviceTokens.Remove(existing);
            return true;
        }

        public async Task RemoveByTokensAsync(IEnumerable<string> tokens)
        {
            var set = tokens.Distinct().ToList();
            if (set.Count == 0) return;

            await _dbContext.DeviceTokens
                .Where(t => set.Contains(t.Token))
                .ExecuteDeleteAsync();
        }

        public async Task<int> RemoveStaleAsync(DateTime olderThan)
        {
            return await _dbContext.DeviceTokens
                .Where(t => t.LastSeenAt < olderThan)
                .ExecuteDeleteAsync();
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
