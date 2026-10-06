using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NoteModule.Domain;
using smart_pet_care_api.Modules.NoteModule.DTOs.Requests;
using smart_pet_care_api.Modules.NoteModule.DTOs.Responses;
using smart_pet_care_api.Modules.NoteModule.Repository;

namespace smart_pet_care_api.Modules.NoteModule.Tests;

internal sealed class FakeNoteRepository : INoteRepository
{
    public bool PetBelongsToUser { get; set; } = true;
    public IReadOnlyList<Note> Notes { get; set; } = [];
    public Note? Note { get; set; }
    public Note? TrackedNote { get; set; }
    public Note? AddedNote { get; private set; }
    public Note? DeletedNote { get; private set; }
    public int SaveChangesCalls { get; private set; }

    public Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId) => Task.FromResult(PetBelongsToUser);

    public Task<IReadOnlyList<Note>> GetByPetIdAsync(Guid petId) => Task.FromResult(Notes);

    public Task<Note?> GetByIdAsync(Guid id) => Task.FromResult(Note);

    public Task<Note?> GetTrackedByIdAsync(Guid id) => Task.FromResult(TrackedNote);

    public Task<Note> AddAsync(Note entity)
    {
        AddedNote = entity;
        return Task.FromResult(entity);
    }

    public void Delete(Note entity) => DeletedNote = entity;

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeNoteService : INoteService
{
    public Func<Guid, Guid, Task<IReadOnlyList<NoteResponseDto>>> GetByPetId { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<NoteResponseDto>>([]);
    public Func<Guid, Guid, Guid, Task<NoteResponseDto?>> GetById { get; set; } =
        (_, _, _) => Task.FromResult<NoteResponseDto?>(new NoteResponseDto());
    public Func<Guid, Guid, CreateNoteDto, Task<NoteResponseDto>> Create { get; set; } =
        (_, _, _) => Task.FromResult(new NoteResponseDto());
    public Func<Guid, Guid, Guid, PatchNoteDto, Task<NoteResponseDto>> Update { get; set; } =
        (_, _, _, _) => Task.FromResult(new NoteResponseDto());
    public Func<Guid, Guid, Guid, Task<bool>> Delete { get; set; } = (_, _, _) => Task.FromResult(true);

    public Task<IReadOnlyList<NoteResponseDto>> GetByPetIdAsync(Guid petId, Guid userId) =>
        GetByPetId(petId, userId);
    public Task<NoteResponseDto?> GetByIdAsync(Guid petId, Guid noteId, Guid userId) =>
        GetById(petId, noteId, userId);
    public Task<NoteResponseDto> CreateAsync(Guid petId, Guid userId, CreateNoteDto dto) =>
        Create(petId, userId, dto);
    public Task<NoteResponseDto> UpdateAsync(Guid petId, Guid noteId, Guid userId, PatchNoteDto dto) =>
        Update(petId, noteId, userId, dto);
    public Task<bool> DeleteAsync(Guid petId, Guid noteId, Guid userId) =>
        Delete(petId, noteId, userId);
}
