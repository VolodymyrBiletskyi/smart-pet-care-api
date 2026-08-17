using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.HealthModule.DTOs.Requests;
using smart_pet_care_api.Modules.HealthModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.Mapper
{
    public static class HealthRecordMapper
    {
        public static HealthRecord ToEntity(CreateHealthRecordDto dto, Guid petId) => new()
        {
            PetId = petId,
            ReminderId = dto.ReminderId,
            Type = dto.Type,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            Symptoms = NormalizeSymptoms(dto.Symptoms),
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
            ReminderId = record.ReminderId,
            Type = record.Type,
            Title = record.Title,
            Description = record.Description,
            Symptoms = record.Symptoms,
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
            if (dto.Symptoms.IsSet) record.Symptoms = NormalizeSymptoms(dto.Symptoms.Value);
            if (dto.PerformedAt.IsSet) record.PerformedAt = NormalizeToUtc(dto.PerformedAt.Value);
            if (dto.NextDueAt.IsSet) record.NextDueAt = dto.NextDueAt.Value is { } nextDue ? NormalizeToUtc(nextDue) : null;
            if (dto.Dosage.IsSet) record.Dosage = dto.Dosage.Value;
            if (dto.Provider.IsSet) record.Provider = dto.Provider.Value;
            record.UpdatedAt = DateTime.UtcNow;
        }

        public static List<SymptomType>? NormalizeSymptoms(List<SymptomType>? symptoms)
        {
            if (symptoms is null || symptoms.Count == 0) return null;
            return symptoms.Distinct().ToList();
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
