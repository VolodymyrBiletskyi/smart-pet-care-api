using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.HealthModule.DTOs.Requests;
using smart_pet_care_api.Modules.HealthModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.HealthModule.Mapper
{
    public static class HealthRecordMapper
    {
        public static HealthRecord ToEntity(CreateHealthRecordDto dto, Guid petId) => new()
        {
            PetId = petId,
            Type = dto.Type,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            PerformedAt = NormalizeToUtc(dto.PerformedAt),
            NextDueAt = dto.NextDueAt is { } nextDue ? NormalizeToUtc(nextDue) : null,
            Dosage = dto.Dosage,
            Provider = dto.Provider,
            CreatedAt = DateTime.UtcNow
        };

        public static HealthRecordResponseDto ToDto(this HealthRecord record) => new()
        {
            Id = record.Id,
            PetId = record.PetId,
            Type = record.Type,
            Title = record.Title,
            Description = record.Description,
            PerformedAt = record.PerformedAt,
            NextDueAt = record.NextDueAt,
            Dosage = record.Dosage,
            Provider = record.Provider,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };

        public static void PatchEntity(this HealthRecord record, PatchHealthRecordDto dto)
        {
            if (dto.Type.IsSet) record.Type = dto.Type.Value;
            if (dto.Title.IsSet) record.Title = dto.Title.Value!.Trim();
            if (dto.Description.IsSet) record.Description = dto.Description.Value;
            if (dto.PerformedAt.IsSet) record.PerformedAt = NormalizeToUtc(dto.PerformedAt.Value);
            if (dto.NextDueAt.IsSet) record.NextDueAt = dto.NextDueAt.Value is { } nextDue ? NormalizeToUtc(nextDue) : null;
            if (dto.Dosage.IsSet) record.Dosage = dto.Dosage.Value;
            if (dto.Provider.IsSet) record.Provider = dto.Provider.Value;
            record.UpdatedAt = DateTime.UtcNow;
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
