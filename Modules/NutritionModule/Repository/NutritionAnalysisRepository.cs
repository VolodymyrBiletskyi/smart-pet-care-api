using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NutritionModule.Repository
{
    public class NutritionAnalysisRepository : INutritionAnalysisRepository
    {
        private readonly AppDbContext _dbContext;

        public NutritionAnalysisRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<NutritionAnalysis>> GetRecentByPetIdAsync(Guid petId, int limit)
        {
            return await OrderedByPetId(_dbContext.NutritionAnalyses.AsNoTracking(), petId)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<NutritionAnalysis>> GetTrackedByPetIdAsync(Guid petId)
        {
            return await OrderedByPetId(_dbContext.NutritionAnalyses, petId).ToListAsync();
        }

        public async Task<NutritionAnalysis> AddAsync(NutritionAnalysis entity)
        {
            await _dbContext.NutritionAnalyses.AddAsync(entity);
            return entity;
        }

        public void DeleteRange(IEnumerable<NutritionAnalysis> entities)
        {
            _dbContext.NutritionAnalyses.RemoveRange(entities);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }

        // Id breaks ties so two analyses stored within the same tick still have
        // a stable "latest".
        private static IOrderedQueryable<NutritionAnalysis> OrderedByPetId(
            IQueryable<NutritionAnalysis> query, Guid petId) =>
            query.Where(a => a.PetId == petId)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id);
    }
}
