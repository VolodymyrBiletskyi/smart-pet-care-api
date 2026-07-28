using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NutritionModule.Repository
{
    public class NutritionGoalRepository : INutritionGoalRepository
    {
        private readonly AppDbContext _dbContext;

        public NutritionGoalRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<NutritionGoal?> GetByPetIdAsync(Guid petId)
        {
            return await _dbContext.NutritionGoals
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.PetId == petId);
        }

        public async Task<NutritionGoal?> GetTrackedByPetIdAsync(Guid petId)
        {
            return await _dbContext.NutritionGoals
                .FirstOrDefaultAsync(g => g.PetId == petId);
        }

        public async Task<NutritionGoal> AddAsync(NutritionGoal entity)
        {
            await _dbContext.NutritionGoals.AddAsync(entity);
            return entity;
        }

        public void Delete(NutritionGoal entity)
        {
            _dbContext.NutritionGoals.Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
