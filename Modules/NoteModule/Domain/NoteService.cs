using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.NoteModule.DTOs.Requests;
using smart_pet_care_api.Modules.NoteModule.DTOs.Responses;
using smart_pet_care_api.Modules.NoteModule.Mapper;
using smart_pet_care_api.Modules.NoteModule.Repository;

namespace smart_pet_care_api.Modules.NoteModule.Domain
{
    public class NoteService : INoteService
    {
        private const int TitleMaxLength = 200;
        private const int ContentMaxLength = 10000;

        private readonly INoteRepository _repo;

        public NoteService(INoteRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<NoteResponseDto>> GetByPetIdAsync(Guid petId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var notes = await _repo.GetByPetIdAsync(petId);
            return notes.Select(n => n.ToDto()).ToList();
        }

        public async Task<NoteResponseDto?> GetByIdAsync(Guid petId, Guid noteId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var note = await _repo.GetByIdAsync(noteId);
            if (note is null || note.PetId != petId) return null;

            return note.ToDto();
        }

        public async Task<NoteResponseDto> CreateAsync(Guid petId, Guid userId, CreateNoteDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            ValidateTitle(dto.Title);
            ValidateContent(dto.Content);

            var note = NoteMapper.ToEntity(dto, petId);

            await _repo.AddAsync(note);
            await _repo.SaveChangesAsync();

            return note.ToDto();
        }

        public async Task<NoteResponseDto> UpdateAsync(Guid petId, Guid noteId, Guid userId, PatchNoteDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            if (!dto.Title.IsSet && !dto.Content.IsSet)
                throw new ValidationException(ErrorCodes.Note.UpdateEmpty, "At least one field must be provided");

            var note = await _repo.GetTrackedByIdAsync(noteId);
            if (note is null || note.PetId != petId)
                throw new NotFoundException(ErrorCodes.Note.NotFound, "Note not found");

            // Validated as the row the patch results in, the way the create path
            // is: both fields are required, so a cleared one has to fail against
            // the note that would remain rather than against the field alone.
            ValidateTitle(dto.Title.IsSet ? dto.Title.Value : note.Title);
            ValidateContent(dto.Content.IsSet ? dto.Content.Value : note.Content);

            note.PatchEntity(dto);

            await _repo.SaveChangesAsync();

            return note.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid petId, Guid noteId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var note = await _repo.GetTrackedByIdAsync(noteId);
            if (note is null || note.PetId != petId) return false;

            _repo.Delete(note);
            await _repo.SaveChangesAsync();

            return true;
        }

        private async Task EnsurePetBelongsToUserAsync(Guid petId, Guid userId)
        {
            var petBelongsToUser = await _repo.PetBelongsToUserAsync(petId, userId);
            if (!petBelongsToUser)
                throw new NotFoundException(ErrorCodes.PetNotFound, "Pet not found");
        }

        private static void ValidateTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ValidationException(ErrorCodes.Note.TitleRequired, "Title is required");

            if (title.Trim().Length > TitleMaxLength)
                throw new ValidationException(ErrorCodes.Note.TitleTooLong, $"Title must be {TitleMaxLength} characters or less", new Dictionary<string, object?> { ["maxLength"] = TitleMaxLength });
        }

        private static void ValidateContent(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ValidationException(ErrorCodes.Note.ContentRequired, "Content is required");

            if (content.Trim().Length > ContentMaxLength)
                throw new ValidationException(ErrorCodes.Note.ContentTooLong, $"Content must be {ContentMaxLength} characters or less", new Dictionary<string, object?> { ["maxLength"] = ContentMaxLength });
        }
    }
}
