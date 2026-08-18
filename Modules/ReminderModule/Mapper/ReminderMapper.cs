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
            IntervalN = r.IntervalN,
            RecalcStrategy = r.RecalcStrategy,
            Days = r.Days,
            Date = r.Date,
            TimeOfDay = r.TimeOfDay,
            StartAt = r.StartAt,
            NextTriggerAt = r.NextTriggerAt,
            EndAt = r.EndAt,
            ScheduleAnchorAt = r.ScheduleAnchorAt,
            OverdueSince = r.OverdueSince,
            LastCompletedAt = r.LastCompletedAt,
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
            PerformedAt = run.PerformedAt,
            Type = run.Type,
            Note = run.Note,
            Status = run.Status,
            Channel = run.Channel,
            CreatedAt = run.CreatedAt
        };

        public static ReminderRunHistoryDto ToHistoryDto(this ReminderRun run, Reminder reminder) => new()
        {
            RunId = run.Id,
            ReminderId = run.ReminderId,
            PetId = reminder.PetId,
            Title = reminder.Title,
            Type = run.Type ?? reminder.Type,
            ScheduledFor = run.ScheduledFor,
            PerformedAt = run.PerformedAt,
            CompletedAt = run.CompletedAt,
            Status = run.Status,
            Note = run.Note
        };

        public static Reminder ToEntity(
            CreateReminderDto dto, DateTime firstTrigger, TimeSpan timeOfDayUtc, RecalcStrategy strategy) => new()
        {
            PetId = dto.PetId,
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            RepeatType = dto.RepeatType,
            IntervalN = dto.IntervalN,
            RecalcStrategy = strategy,
            // Weekly needs days to fire at all; the other repeat types only keep them as the
            // alignment target of the weekday-aligned strategy.
            Days = KeepDays(dto.RepeatType, strategy) ? dto.Days : [],
            Date = dto.RepeatType is RepeatType.Monthly or RepeatType.Once ? dto.Date : null,
            TimeOfDay = timeOfDayUtc,
            UtcOffsetMinutes = dto.UtcOffsetMinutes,
            StartAt = firstTrigger,
            NextTriggerAt = firstTrigger,
            // Intervals count from the first occurrence, so "every 2 weeks" means every second
            // week starting there rather than from an arbitrary creation timestamp.
            ScheduleAnchorAt = firstTrigger,
            EndAt = dto.EndAt is { } endAt ? NormalizeToUtc(endAt) : null,
            SourceType = SourceType.Manual
        };

        public static bool KeepDays(RepeatType repeatType, RecalcStrategy strategy) =>
            repeatType == RepeatType.Weekly
            || strategy == RecalcStrategy.FromCompletionAlignedToWeekday;

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
