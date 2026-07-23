using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.JournalModule.DTOs.Requests;
using smart_pet_care_api.Modules.JournalModule.DTOs.Responses;
using smart_pet_care_api.Modules.JournalModule.Mapper;
using smart_pet_care_api.Modules.JournalModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.JournalModule.Domain
{
    public class JournalEntryService : IJournalEntryService
    {
        private readonly IJournalEntryRepository _repo;

        public JournalEntryService(IJournalEntryRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<JournalEntryResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, JournalEntryType? type, JournalEntrySeverity? severity, DateTime? from, DateTime? to)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            if (type.HasValue) ValidateType(type.Value);
            if (severity.HasValue) ValidateSeverity(severity.Value);

            var fromUtc = from is { } f ? JournalEntryMapper.NormalizeToUtc(f) : (DateTime?)null;
            var toUtc = to is { } t ? JournalEntryMapper.NormalizeToUtc(t) : (DateTime?)null;

            if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
                throw new ArgumentException("From cannot be later than To");

            var entries = await _repo.GetByPetIdAsync(petId, type, severity, fromUtc, toUtc);
            return entries.Select(e => e.ToDto()).ToList();
        }

        public async Task<JournalEntryResponseDto?> GetByIdAsync(Guid petId, Guid entryId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var entry = await _repo.GetByIdAsync(entryId);
            if (entry is null || entry.PetId != petId) return null;

            return entry.ToDto();
        }

        public async Task<JournalEntryResponseDto> CreateAsync(Guid petId, Guid userId, CreateJournalEntryDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            ValidateCreate(dto);

            var entry = JournalEntryMapper.ToEntity(dto, petId);

            await _repo.AddAsync(entry);
            await _repo.SaveChangesAsync();

            return entry.ToDto();
        }

        public async Task<JournalEntryResponseDto> UpdateAsync(Guid petId, Guid entryId, Guid userId, PatchJournalEntryDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            ValidatePatch(dto);

            var entry = await _repo.GetTrackedByIdAsync(entryId);
            if (entry is null || entry.PetId != petId)
                throw new InvalidOperationException("Journal entry not found");

            entry.PatchEntity(dto);

            await _repo.SaveChangesAsync();

            return entry.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid petId, Guid entryId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var entry = await _repo.GetTrackedByIdAsync(entryId);
            if (entry is null || entry.PetId != petId) return false;

            _repo.Delete(entry);
            await _repo.SaveChangesAsync();

            return true;
        }

        private async Task EnsurePetBelongsToUserAsync(Guid petId, Guid userId)
        {
            var petBelongsToUser = await _repo.PetBelongsToUserAsync(petId, userId);
            if (!petBelongsToUser)
                throw new InvalidOperationException("Pet not found");
        }

        private static void ValidateCreate(CreateJournalEntryDto dto)
        {
            ValidateType(dto.Type);
            if (dto.Severity.HasValue) ValidateSeverity(dto.Severity.Value);
            ValidateTitle(dto.Title);
            ValidateObservedAt(dto.ObservedAt);
            ValidateNotes(dto.Notes);
        }

        private static void ValidatePatch(PatchJournalEntryDto dto)
        {
            if (dto.Type.IsSet) ValidateType(dto.Type.Value);
            if (dto.Severity.IsSet && dto.Severity.Value.HasValue) ValidateSeverity(dto.Severity.Value.Value);
            if (dto.Title.IsSet) ValidateTitle(dto.Title.Value);
            if (dto.ObservedAt.IsSet) ValidateObservedAt(dto.ObservedAt.Value);
            if (dto.Notes.IsSet) ValidateNotes(dto.Notes.Value);
        }

        private static void ValidateType(JournalEntryType type)
        {
            if (!Enum.IsDefined(type))
                throw new ArgumentException("Type is invalid");
        }

        private static void ValidateSeverity(JournalEntrySeverity severity)
        {
            if (!Enum.IsDefined(severity))
                throw new ArgumentException("Severity is invalid");
        }

        private static void ValidateTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required");

            if (title.Trim().Length > 200)
                throw new ArgumentException("Title must be 200 characters or less");
        }

        private static void ValidateObservedAt(DateTime observedAt)
        {
            if (JournalEntryMapper.NormalizeToUtc(observedAt) > DateTime.UtcNow.AddMinutes(10))
                throw new ArgumentException("ObservedAt cannot be in the future");
        }

        private static void ValidateNotes(string? notes)
        {
            if (notes is { Length: > 4000 })
                throw new ArgumentException("Notes must be 4000 characters or less");
        }
    }
}
