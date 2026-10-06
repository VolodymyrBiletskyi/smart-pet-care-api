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
                Title = Normalize(dto.Title),
                Content = Normalize(dto.Content),
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
            if (dto.Title.IsSet) note.Title = Normalize(dto.Title.Value);
            if (dto.Content.IsSet) note.Content = Normalize(dto.Content.Value);
            note.UpdatedAt = DateTime.UtcNow;
        }

        // A blank field is stored as null rather than "": the two mean the same
        // thing here, and one of them keeps the absent case to a single check.
        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
