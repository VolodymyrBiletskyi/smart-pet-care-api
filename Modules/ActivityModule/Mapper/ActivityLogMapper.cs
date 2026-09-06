using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Mapper
{
    public static class ActivityLogMapper
    {
        public static ActivityLog ToEntity(ActivityReading reading, Guid petId, ActivitySource source) => new()
        {
            PetId = petId,
            RecordedAt = NormalizeToUtc(reading.RecordedAt),
            Steps = reading.Steps,
            Type = reading.Type,
            Intensity = reading.Intensity,
            DurationMinutes = reading.DurationMinutes,
            Location = reading.Location,
            Note = reading.Note,
            Source = source,
            CreatedAt = DateTime.UtcNow
        };

        public static ActivityLogResponseDto ToDto(this ActivityLog log) => new()
        {
            Id = log.Id,
            PetId = log.PetId,
            RecordedAt = log.RecordedAt,
            Steps = log.Steps,
            Type = log.Type,
            Intensity = log.Intensity,
            DurationMinutes = log.DurationMinutes,
            ActiveMinutes = ActivityEffort.ActiveMinutes(log.DurationMinutes, log.Intensity),
            Location = log.Location,
            Note = log.Note,
            Source = log.Source,
            CreatedAt = log.CreatedAt,
            UpdatedAt = log.UpdatedAt
        };

        /// <summary>
        /// The row as the service's validator wants to see it. Reusing <see cref="ActivityReading"/>
        /// is what keeps an edited log answering to the same rules as a freshly read one.
        /// </summary>
        public static ActivityReading ToReading(this ActivityLog log) => new(
            log.RecordedAt,
            log.Steps,
            log.Location,
            log.Note,
            log.Type,
            log.Intensity,
            log.DurationMinutes);

        public static void PatchEntity(this ActivityLog log, PatchActivityLogDto dto)
        {
            if (dto.RecordedAt.IsSet) log.RecordedAt = NormalizeToUtc(dto.RecordedAt.Value);
            if (dto.Steps.IsSet) log.Steps = dto.Steps.Value;
            if (dto.Type.IsSet) log.Type = dto.Type.Value;
            if (dto.Intensity.IsSet) log.Intensity = dto.Intensity.Value;
            if (dto.DurationMinutes.IsSet) log.DurationMinutes = dto.DurationMinutes.Value;
            if (dto.Location.IsSet) log.Location = Trim(dto.Location.Value);
            if (dto.Note.IsSet) log.Note = Trim(dto.Note.Value);

            log.UpdatedAt = DateTime.UtcNow;
        }

        public static DateTime NormalizeToUtc(DateTime dateTime) =>
            dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
