using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NutritionModule.Repository
{
    public interface INutritionGoalRepository
    {
        Task<NutritionGoal?> GetByPetIdAsync(Guid petId);
        Task<NutritionGoal?> GetTrackedByPetIdAsync(Guid petId);
        Task<NutritionGoal> AddAsync(NutritionGoal entity);
        void Delete(NutritionGoal entity);
        Task<int> SaveChangesAsync();
    }
}
