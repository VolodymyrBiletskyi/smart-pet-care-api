using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
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
            Location = log.Location,
            Note = log.Note,
            Source = log.Source,
            CreatedAt = log.CreatedAt
        };

        public static DateTime NormalizeToUtc(DateTime dateTime) =>
            dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
    }
}
