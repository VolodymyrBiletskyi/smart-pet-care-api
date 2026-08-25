using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Repository
{
    public class ActivityLogRepository : IActivityLogRepository
    {
        private readonly AppDbContext _dbContext;

        public ActivityLogRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId)
        {
            return await _dbContext.Pets.AnyAsync(p => p.Id == petId && p.UserId == userId);
        }

        public async Task<IReadOnlyList<ActivityLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null, ActivitySource? source = null)
        {
            var query = _dbContext.ActivityLogs
                .AsNoTracking()
                .Where(a => a.PetId == petId);

            if (from.HasValue)
                query = query.Where(a => a.RecordedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.RecordedAt <= to.Value);

            if (source.HasValue)
                query = query.Where(a => a.Source == source.Value);

            return await query
                .OrderByDescending(a => a.RecordedAt)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<ActivityLog?> GetByIdAsync(Guid id)
        {
            return await _dbContext.ActivityLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<ActivityLog?> GetTrackedByIdAsync(Guid id)
        {
            return await _dbContext.ActivityLogs
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<ActivityLog> AddAsync(ActivityLog entity)
        {
            await _dbContext.ActivityLogs.AddAsync(entity);
            return entity;
        }

        public void Delete(ActivityLog entity)
        {
            _dbContext.ActivityLogs.Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
