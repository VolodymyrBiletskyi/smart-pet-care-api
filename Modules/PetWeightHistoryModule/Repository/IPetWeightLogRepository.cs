using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Repository
{
    public interface IPetWeightLogRepository
    {
        Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId);
        Task<IReadOnlyList<PetWeightLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null);
        Task<PetWeightLog?> GetTrackedByIdAsync(Guid id);
        Task<PetWeightLog?> GetLatestByPetIdAsync(Guid petId);
        Task<Pet?> GetTrackedPetByIdAsync(Guid petId);
        Task<bool> ExistsForPetAtMeasuredAtAsync(Guid petId, DateTime measuredAt, Guid? excludeId = null);
        Task<PetWeightLog> AddAsync(PetWeightLog entity);
        void Delete(PetWeightLog entity);
        Task<int> SaveChangesAsync();
    }
}
