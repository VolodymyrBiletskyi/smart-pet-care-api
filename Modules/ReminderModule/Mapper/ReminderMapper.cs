using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Mapper
{
    public static class ReminderMapper
    {
        public static ReminderResponseDto ToDto(this Reminder r) => new()
        {
            Id = r.Id,
            PetId = r.PetId,
            PetSpecies = r.Pet?.Species,
            Title = r.Title,
            Description = r.Description,
            Type = r.Type,
            Status = r.Status,
            RepeatType = r.RepeatType,
            Days = r.Days,
            Date = r.Date,
            TimeOfDay = r.TimeOfDay,
            StartAt = r.StartAt,
            NextTriggerAt = r.NextTriggerAt,
            EndAt = r.EndAt,
            IsSystemGenerated = r.IsSystemGenerated,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };

        public static ReminderRunResponseDto ToDto(this ReminderRun run) => new()
        {
            Id = run.Id,
            ReminderId = run.ReminderId,
            ScheduledFor = run.ScheduledFor,
            SentAt = run.SentAt,
            CompletedAt = run.CompletedAt,
            Status = run.Status,
            Channel = run.Channel,
            CreatedAt = run.CreatedAt
        };

        public static Reminder ToEntity(CreateReminderDto dto, DateTime firstTrigger, TimeSpan timeOfDayUtc) => new()
        {
            PetId = dto.PetId,
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            RepeatType = dto.RepeatType,
            Days = dto.RepeatType == RepeatType.Weekly ? dto.Days : [],
            Date = dto.RepeatType is RepeatType.Monthly or RepeatType.Once ? dto.Date : null,
            TimeOfDay = timeOfDayUtc,
            UtcOffsetMinutes = dto.UtcOffsetMinutes,
            StartAt = firstTrigger,
            NextTriggerAt = firstTrigger,
            EndAt = dto.EndAt is { } endAt ? NormalizeToUtc(endAt) : null,
            SourceType = SourceType.Manual
        };

        public static void PatchEntity(this Reminder reminder, PatchReminderDto dto)
        {
            if (dto.Title != null) reminder.Title = dto.Title;
            if (dto.Description != null) reminder.Description = dto.Description;
            if (dto.EndAt.HasValue) reminder.EndAt = NormalizeToUtc(dto.EndAt.Value);
            if (dto.Status.HasValue) reminder.Status = dto.Status.Value;
            reminder.UpdatedAt = DateTime.UtcNow;
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
