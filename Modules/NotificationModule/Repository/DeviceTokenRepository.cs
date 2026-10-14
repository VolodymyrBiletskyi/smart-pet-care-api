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

        public async Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId)
        {
            return await _dbContext.DeviceTokens
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .ToListAsync();
        }

        /// <summary>
        /// Registers the token, moving it to this user if another account registered it first.
        /// Reassignment rather than a second row is the point: the device has one token, and
        /// whoever is signed in on it now is the only account whose pushes belong there.
        /// </summary>
        public async Task UpsertAsync(DeviceToken token)
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "DeviceTokens" ("Id", "UserId", "Token", "Platform", "CreatedAt", "LastSeenAt")
                VALUES ({token.Id}, {token.UserId}, {token.Token}, {(int)token.Platform}, {token.CreatedAt}, {token.LastSeenAt})
                ON CONFLICT ("Token")
                DO UPDATE SET
                    "UserId" = EXCLUDED."UserId",
                    "LastSeenAt" = EXCLUDED."LastSeenAt",
                    "Platform" = EXCLUDED."Platform"
                """);
        }

        public async Task<bool> RemoveByUserAndTokenAsync(Guid userId, string token)
        {
            // Deleted in one statement rather than loaded and tracked: the push sender
            // prunes invalid tokens from its own scope, and a row that goes between the
            // select and the save is a DbUpdateConcurrencyException out of SaveChanges.
            return await _dbContext.DeviceTokens
                .Where(t => t.UserId == userId && t.Token == token)
                .ExecuteDeleteAsync() > 0;
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
