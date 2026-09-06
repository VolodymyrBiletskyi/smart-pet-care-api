using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Mapper
{
    public static class SleepLogMapper
    {
        public static SleepLog ToEntity(CreateSleepLogDto dto, Guid petId) => new()
        {
            PetId = petId,
            SleepDate = NormalizeToDate(dto.SleepDate),
            Hours = dto.Hours,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            Source = ActivitySource.Manual,
            CreatedAt = DateTime.UtcNow
        };

        public static SleepLogResponseDto ToDto(this SleepLog log) => new()
        {
            Id = log.Id,
            PetId = log.PetId,
            SleepDate = log.SleepDate,
            Hours = log.Hours,
            Note = log.Note,
            Source = log.Source,
            CreatedAt = log.CreatedAt,
            UpdatedAt = log.UpdatedAt
        };

        public static void PatchEntity(this SleepLog log, PatchSleepLogDto dto)
        {
            if (dto.SleepDate.IsSet) log.SleepDate = NormalizeToDate(dto.SleepDate.Value);
            if (dto.Hours.IsSet) log.Hours = dto.Hours.Value;
            if (dto.Note.IsSet)
                log.Note = string.IsNullOrWhiteSpace(dto.Note.Value) ? null : dto.Note.Value.Trim();

            log.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Drops the time of day and pins the result to UTC midnight. The date the caller
        /// sent is taken at face value: it is the pet's local day, and converting it to UTC
        /// would move roughly half the world's evenings onto the wrong date.
        /// </summary>
        public static DateTime NormalizeToDate(DateTime dateTime) =>
            DateTime.SpecifyKind(dateTime.Date, DateTimeKind.Utc);
    }
}
