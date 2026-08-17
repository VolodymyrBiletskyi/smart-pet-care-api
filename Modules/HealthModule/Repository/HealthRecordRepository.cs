using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.Repository
{
    public class HealthRecordRepository : IHealthRecordRepository
    {
        private readonly AppDbContext _dbContext;

        public HealthRecordRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId)
        {
            return await _dbContext.Pets.AnyAsync(p => p.Id == petId && p.UserId == userId);
        }

        public async Task<IReadOnlyList<HealthRecord>> GetByPetIdAsync(Guid petId, HealthRecordType? type, SymptomType? symptom, DateTime? from, DateTime? to)
        {
            var query = _dbContext.HealthRecords
                .AsNoTracking()
                .Where(r => r.PetId == petId);

            if (type.HasValue)
                query = query.Where(r => r.Type == type.Value);

            if (symptom.HasValue)
                query = query.Where(r => r.Symptoms != null && r.Symptoms.Contains(symptom.Value));

            if (from.HasValue)
                query = query.Where(r => r.PerformedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(r => r.PerformedAt <= to.Value);

            return await query
                .OrderByDescending(r => r.PerformedAt)
                .ToListAsync();
        }

        public async Task<HealthRecord?> GetByIdAsync(Guid id)
        {
            return await _dbContext.HealthRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<HealthRecord?> GetTrackedByIdAsync(Guid id)
        {
            return await _dbContext.HealthRecords
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<HealthRecord?> GetByReminderAndPerformedAtAsync(Guid reminderId, DateTime performedAtUtc)
        {
            return await _dbContext.HealthRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReminderId == reminderId && r.PerformedAt == performedAtUtc);
        }

        public async Task<HealthRecord> AddAsync(HealthRecord entity)
        {
            await _dbContext.HealthRecords.AddAsync(entity);
            return entity;
        }

        public void Delete(HealthRecord entity)
        {
            _dbContext.HealthRecords.Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
