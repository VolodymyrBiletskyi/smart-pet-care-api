using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.ActivityModule.Repository
{
    public class SleepLogRepository : ISleepLogRepository
    {
        private readonly AppDbContext _dbContext;

        public SleepLogRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId)
        {
            return await _dbContext.Pets.AnyAsync(p => p.Id == petId && p.UserId == userId);
        }

        public async Task<IReadOnlyList<SleepLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null)
        {
            var query = _dbContext.SleepLogs
                .AsNoTracking()
                .Where(s => s.PetId == petId);

            if (from.HasValue)
                query = query.Where(s => s.SleepDate >= from.Value);

            if (to.HasValue)
                query = query.Where(s => s.SleepDate <= to.Value);

            return await query
                .OrderByDescending(s => s.SleepDate)
                .ThenByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<SleepLog?> GetByIdAsync(Guid id)
        {
            return await _dbContext.SleepLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<SleepLog?> GetTrackedByIdAsync(Guid id)
        {
            return await _dbContext.SleepLogs
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<decimal> GetLoggedHoursAsync(Guid petId, DateTime sleepDate, Guid? excludeSleepLogId = null)
        {
            var query = _dbContext.SleepLogs
                .AsNoTracking()
                .Where(s => s.PetId == petId && s.SleepDate == sleepDate);

            if (excludeSleepLogId is { } excludedId)
                query = query.Where(s => s.Id != excludedId);

            return await query.SumAsync(s => (decimal?)s.Hours) ?? 0m;
        }

        public async Task<SleepLog> AddAsync(SleepLog entity)
        {
            await _dbContext.SleepLogs.AddAsync(entity);
            return entity;
        }

        public void Delete(SleepLog entity)
        {
            _dbContext.SleepLogs.Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
