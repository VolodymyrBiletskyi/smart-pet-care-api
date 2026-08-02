using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NutritionModule.Repository
{
    public interface INutritionAnalysisRepository
    {
        /// <summary>Newest first.</summary>
        Task<IReadOnlyList<NutritionAnalysis>> GetRecentByPetIdAsync(Guid petId, int limit);

        /// <summary>Newest first, tracked, for retention trimming.</summary>
        Task<IReadOnlyList<NutritionAnalysis>> GetTrackedByPetIdAsync(Guid petId);

        Task<NutritionAnalysis> AddAsync(NutritionAnalysis entity);
        void DeleteRange(IEnumerable<NutritionAnalysis> entities);
        Task<int> SaveChangesAsync();
    }
}
