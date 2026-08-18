using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.Repository
{
    public interface IHealthRecordRepository
    {
        Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId);
        Task<IReadOnlyList<HealthRecord>> GetByPetIdAsync(Guid petId, HealthRecordType? type, SymptomType? symptom, DateTime? from, DateTime? to);
        Task<HealthRecord?> GetByIdAsync(Guid id);
        Task<HealthRecord?> GetTrackedByIdAsync(Guid id);

        /// <summary>
        /// Record already filed for this reminder on this date, used to recognise a completion
        /// that arrives twice instead of filing it again.
        /// </summary>
        Task<HealthRecord?> GetByReminderAndPerformedAtAsync(Guid reminderId, DateTime performedAtUtc);
        Task<HealthRecord> AddAsync(HealthRecord entity);
        void Delete(HealthRecord entity);
        Task<int> SaveChangesAsync();
    }
}
