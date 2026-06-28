using smart_pet_care_api.Models;
namespace smart_pet_care_api.Modules.PetModule.Repository
{
    public interface IPetRepository
    {
        Task<IReadOnlyList<Pet>> GetByUserIdAsync(Guid userId);
        Task<Pet?> GetByIdAndUserIdAsync(Guid id, Guid userId);
        Task<Pet?> GetTrackedByIdAndUserIdAsync(Guid id, Guid userId);
        Task<bool> ExistsForUserAsync(Guid id, Guid userId);
        Task<Pet> AddAsync(Pet entity);
        Task<int> SaveChangesAsync();
        void Delete(Pet pet);
    }
}
