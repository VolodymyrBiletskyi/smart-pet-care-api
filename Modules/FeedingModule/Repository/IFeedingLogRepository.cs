using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.FeedingModule.Repository
{
    public interface IFeedingLogRepository
    {
        Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId);
        Task<IReadOnlyList<FeedingLog>> GetByPetIdAsync(Guid petId);
        Task<FeedingLog?> GetByIdAsync(Guid id);
        Task<FeedingLog?> GetTrackedByIdAsync(Guid id);
        Task<FeedingLog> AddAsync(FeedingLog entity);
        void Delete(FeedingLog entity);
        Task<int> SaveChangesAsync();
    }
}
