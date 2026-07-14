using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Repository
{
    public class PetWeightLogRepository : IPetWeightLogRepository
    {
        private readonly AppDbContext _dbContext;

        public PetWeightLogRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId)
        {
            return await _dbContext.Pets.AnyAsync(p => p.Id == petId && p.UserId == userId);
        }

        public async Task<IReadOnlyList<PetWeightLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null)
        {
            var query = _dbContext.PetWeightLogs
                .AsNoTracking()
                .Where(w => w.PetId == petId);

            if (from.HasValue)
                query = query.Where(w => w.MeasuredAt >= from.Value);

            if (to.HasValue)
                query = query.Where(w => w.MeasuredAt <= to.Value);

            return await query
                .OrderByDescending(w => w.MeasuredAt)
                .ThenByDescending(w => w.CreatedAt)
                .ToListAsync();
        }

        public async Task<PetWeightLog?> GetTrackedByIdAsync(Guid id)
        {
            return await _dbContext.PetWeightLogs
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<PetWeightLog?> GetLatestByPetIdAsync(Guid petId)
        {
            return await _dbContext.PetWeightLogs
                .AsNoTracking()
                .Where(w => w.PetId == petId)
                .OrderByDescending(w => w.MeasuredAt)
                .ThenByDescending(w => w.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<Pet?> GetTrackedPetByIdAsync(Guid petId)
        {
            return await _dbContext.Pets.FirstOrDefaultAsync(p => p.Id == petId);
        }

        public async Task<bool> ExistsForPetAtMeasuredAtAsync(Guid petId, DateTime measuredAt, Guid? excludeId = null)
        {
            return await _dbContext.PetWeightLogs
                .AsNoTracking()
                .AnyAsync(w =>
                    w.PetId == petId &&
                    w.MeasuredAt == measuredAt &&
                    (!excludeId.HasValue || w.Id != excludeId.Value));
        }

        public async Task<PetWeightLog> AddAsync(PetWeightLog entity)
        {
            await _dbContext.PetWeightLogs.AddAsync(entity);
            return entity;
        }

        public void Delete(PetWeightLog entity)
        {
            _dbContext.PetWeightLogs.Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
