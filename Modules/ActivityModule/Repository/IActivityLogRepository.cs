using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Repository
{
    public interface IActivityLogRepository
    {
        Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId);
        Task<IReadOnlyList<ActivityLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null, ActivitySource? source = null);
        Task<ActivityLog?> GetByIdAsync(Guid id);
        Task<ActivityLog?> GetTrackedByIdAsync(Guid id);
        Task<ActivityLog> AddAsync(ActivityLog entity);
        void Delete(ActivityLog entity);
        Task<int> SaveChangesAsync();
    }
}
