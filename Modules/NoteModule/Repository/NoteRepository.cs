using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NoteModule.Repository
{
    public class NoteRepository : INoteRepository
    {
        private readonly AppDbContext _dbContext;

        public NoteRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId)
        {
            return await _dbContext.Pets.AnyAsync(p => p.Id == petId && p.UserId == userId);
        }

        public async Task<IReadOnlyList<Note>> GetByPetIdAsync(Guid petId)
        {
            return await _dbContext.Notes
                .AsNoTracking()
                .Where(n => n.PetId == petId)
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();
        }

        public async Task<Note?> GetByIdAsync(Guid id)
        {
            return await _dbContext.Notes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == id);
        }

        public async Task<Note?> GetTrackedByIdAsync(Guid id)
        {
            return await _dbContext.Notes
                .FirstOrDefaultAsync(n => n.Id == id);
        }

        public async Task<Note> AddAsync(Note entity)
        {
            await _dbContext.Notes.AddAsync(entity);
            return entity;
        }

        public void Delete(Note entity)
        {
            _dbContext.Notes.Remove(entity);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
