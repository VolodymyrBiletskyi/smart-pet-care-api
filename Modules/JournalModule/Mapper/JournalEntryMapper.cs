using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.JournalModule.DTOs.Requests;
using smart_pet_care_api.Modules.JournalModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.JournalModule.Mapper
{
    public static class JournalEntryMapper
    {
        public static JournalEntry ToEntity(CreateJournalEntryDto dto, Guid petId) => new()
        {
            PetId = petId,
            Type = dto.Type,
            Title = dto.Title.Trim(),
            Notes = dto.Notes,
            Severity = dto.Severity,
            ObservedAt = NormalizeToUtc(dto.ObservedAt),
            CreatedAt = DateTime.UtcNow
        };

        public static JournalEntryResponseDto ToDto(this JournalEntry entry) => new()
        {
            Id = entry.Id,
            PetId = entry.PetId,
            Type = entry.Type,
            Title = entry.Title,
            Notes = entry.Notes,
            Severity = entry.Severity,
            ObservedAt = entry.ObservedAt,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };

        public static void PatchEntity(this JournalEntry entry, PatchJournalEntryDto dto)
        {
            if (dto.Type.IsSet) entry.Type = dto.Type.Value;
            if (dto.Title.IsSet) entry.Title = dto.Title.Value!.Trim();
            if (dto.Notes.IsSet) entry.Notes = dto.Notes.Value;
            if (dto.Severity.IsSet) entry.Severity = dto.Severity.Value;
            if (dto.ObservedAt.IsSet) entry.ObservedAt = NormalizeToUtc(dto.ObservedAt.Value);
            entry.UpdatedAt = DateTime.UtcNow;
        }

        public static DateTime NormalizeToUtc(DateTime dateTime) =>
            dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
    }
}
