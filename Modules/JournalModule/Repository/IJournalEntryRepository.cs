using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.JournalModule.Repository
{
    public interface IJournalEntryRepository
    {
        Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId);
        Task<IReadOnlyList<JournalEntry>> GetByPetIdAsync(Guid petId, JournalEntryType? type, JournalEntrySeverity? severity, DateTime? from, DateTime? to);
        Task<JournalEntry?> GetByIdAsync(Guid id);
        Task<JournalEntry?> GetTrackedByIdAsync(Guid id);
        Task<JournalEntry> AddAsync(JournalEntry entity);
        void Delete(JournalEntry entity);
        Task<int> SaveChangesAsync();
    }
}
