using smart_pet_care_api.Modules.NoteModule.DTOs.Requests;
using smart_pet_care_api.Modules.NoteModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NoteModule.Domain
{
    public interface INoteService
    {
        Task<IReadOnlyList<NoteResponseDto>> GetByPetIdAsync(Guid petId, Guid userId);
        Task<NoteResponseDto?> GetByIdAsync(Guid petId, Guid noteId, Guid userId);
        Task<NoteResponseDto> CreateAsync(Guid petId, Guid userId, CreateNoteDto dto);
        Task<NoteResponseDto> UpdateAsync(Guid petId, Guid noteId, Guid userId, PatchNoteDto dto);
        Task<bool> DeleteAsync(Guid petId, Guid noteId, Guid userId);
    }
}
