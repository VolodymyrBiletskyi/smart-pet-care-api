using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NoteModule.Repository
{
    public interface INoteRepository
    {
        Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId);
        Task<IReadOnlyList<Note>> GetByPetIdAsync(Guid petId);
        Task<Note?> GetByIdAsync(Guid id);
        Task<Note?> GetTrackedByIdAsync(Guid id);
        Task<Note> AddAsync(Note entity);
        void Delete(Note entity);
        Task<int> SaveChangesAsync();
    }
}
