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
            Title = r.Title,
            Description = r.Description,
            Type = r.Type,
            Status = r.Status,
            Days = r.Days,
            IsRepeatable = r.IsRepeatable,
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

        public static Reminder ToEntity(CreateReminderDto dto, DateTime firstTrigger) => new()
        {
            PetId = dto.PetId,
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            Days = dto.Days,
            TimeOfDay = dto.Time.ToTimeSpan(),
            IsRepeatable = dto.IsRepeatable,
            StartAt = firstTrigger,
            NextTriggerAt = firstTrigger,
            EndAt = dto.EndAt,
            SourceType = SourceType.Manual
        };

        public static void PatchEntity(this Reminder reminder, PatchReminderDto dto)
        {
            if (dto.Title != null) reminder.Title = dto.Title;
            if (dto.Description != null) reminder.Description = dto.Description;
            if (dto.Days != null) reminder.Days = dto.Days;
            if (dto.Time.HasValue) reminder.TimeOfDay = dto.Time.Value.ToTimeSpan();
            if (dto.IsRepeatable.HasValue) reminder.IsRepeatable = dto.IsRepeatable.Value;
            if (dto.EndAt.HasValue) reminder.EndAt = dto.EndAt;
            if (dto.Status.HasValue) reminder.Status = dto.Status.Value;
            reminder.UpdatedAt = DateTime.UtcNow;
        }
    }
}
