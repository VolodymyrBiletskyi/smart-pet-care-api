using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.FeedingModule.Repository
{
    public class FeedingLogRepository : IFeedingLogRepository
    {
        private readonly AppDbContext _dbContext;

        public FeedingLogRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId)
        {
            return await _dbContext.Pets.AnyAsync(p => p.Id == petId && p.UserId == userId);
        }

        public async Task<IReadOnlyList<FeedingLog>> GetByPetIdAsync(Guid petId)
        {
            return await _dbContext.FeedingLogs
                .AsNoTracking()
                .Where(f => f.PetId == petId)
                .OrderByDescending(f => f.FedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<FeedingLog>> GetByPetIdAndRangeAsync(Guid petId, DateTime startUtc, DateTime endUtc)
        {
            return await _dbContext.FeedingLogs
                .AsNoTracking()
                .Where(f => f.PetId == petId && f.FedAt >= startUtc && f.FedAt < endUtc)
                .OrderBy(f => f.FedAt)
                .ToListAsync();
        }

        public async Task<FeedingLog?> GetByIdAsync(Guid id)
        {
            return await _dbContext.FeedingLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<FeedingLog?> GetTrackedByIdAsync(Guid id)
        {
            return await _dbContext.FeedingLogs
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<FeedingLog> AddAsync(FeedingLog entity)
        {
            await _dbContext.FeedingLogs.AddAsync(entity);
            return entity;
        }

        public void Delete(FeedingLog entity)
        {
            _dbContext.FeedingLogs.Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
