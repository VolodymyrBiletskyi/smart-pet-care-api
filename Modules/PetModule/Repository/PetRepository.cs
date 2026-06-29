using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.PetModule.Repository
{
    public class PetRepository : IPetRepository
    {
        private readonly AppDbContext _dbContext;

        public PetRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Pet?> GetByIdAsync(Guid id)
        {
            return await _dbContext.Pets
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Pet?> GetByIdAndUserIdAsync(Guid id, Guid userId)
        {
            return await _dbContext.Pets
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        }

        public async Task<Pet?> GetTrackedByIdAndUserIdAsync(Guid id, Guid userId)
        {
            return await _dbContext.Pets
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        }

        public async Task<IReadOnlyList<Pet>> GetByUserIdAsync(Guid userId)
        {
            return await _dbContext.Pets
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .ToListAsync();
        }

        public async Task<Pet> AddAsync(Pet entity)
        {
            await _dbContext.Pets.AddAsync(entity);
            return entity;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }

        public void Delete(Pet pet)
        {
            _dbContext.Pets.Remove(pet);
        }

        public async Task<bool> ExistsForUserAsync(Guid petId, Guid userId)
        {
            return await _dbContext.Pets.AnyAsync(p => p.Id == petId && p.UserId == userId);
        }
    }
}
