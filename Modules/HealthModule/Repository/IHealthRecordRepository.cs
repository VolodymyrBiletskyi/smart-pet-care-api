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
        Task<HealthRecord> AddAsync(HealthRecord entity);
        void Delete(HealthRecord entity);
        Task<int> SaveChangesAsync();
    }
}
