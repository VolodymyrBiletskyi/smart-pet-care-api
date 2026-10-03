using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NoteModule.DTOs.Requests;
using smart_pet_care_api.Modules.NoteModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NoteModule.Mapper
{
    public static class NoteMapper
    {
        public static Note ToEntity(CreateNoteDto dto, Guid petId)
        {
            var now = DateTime.UtcNow;

            return new Note
            {
                PetId = petId,
                Title = dto.Title.Trim(),
                Content = dto.Content.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        public static NoteResponseDto ToDto(this Note note) => new()
        {
            Id = note.Id,
            PetId = note.PetId,
            Title = note.Title,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };

        public static void PatchEntity(this Note note, PatchNoteDto dto)
        {
            if (dto.Title.IsSet) note.Title = dto.Title.Value!.Trim();
            if (dto.Content.IsSet) note.Content = dto.Content.Value!.Trim();
            note.UpdatedAt = DateTime.UtcNow;
        }
    }
}
