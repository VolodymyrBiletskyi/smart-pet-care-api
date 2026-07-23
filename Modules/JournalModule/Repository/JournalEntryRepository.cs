using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.JournalModule.Repository
{
    public class JournalEntryRepository : IJournalEntryRepository
    {
        private readonly AppDbContext _dbContext;

        public JournalEntryRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId)
        {
            return await _dbContext.Pets.AnyAsync(p => p.Id == petId && p.UserId == userId);
        }

        public async Task<IReadOnlyList<JournalEntry>> GetByPetIdAsync(Guid petId, JournalEntryType? type, JournalEntrySeverity? severity, DateTime? from, DateTime? to)
        {
            var query = _dbContext.JournalEntries
                .AsNoTracking()
                .Where(e => e.PetId == petId);

            if (type.HasValue)
                query = query.Where(e => e.Type == type.Value);

            if (severity.HasValue)
                query = query.Where(e => e.Severity == severity.Value);

            if (from.HasValue)
                query = query.Where(e => e.ObservedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.ObservedAt <= to.Value);

            return await query
                .OrderByDescending(e => e.ObservedAt)
                .ToListAsync();
        }

        public async Task<JournalEntry?> GetByIdAsync(Guid id)
        {
            return await _dbContext.JournalEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<JournalEntry?> GetTrackedByIdAsync(Guid id)
        {
            return await _dbContext.JournalEntries
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<JournalEntry> AddAsync(JournalEntry entity)
        {
            await _dbContext.JournalEntries.AddAsync(entity);
            return entity;
        }

        public void Delete(JournalEntry entity)
        {
            _dbContext.JournalEntries.Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
