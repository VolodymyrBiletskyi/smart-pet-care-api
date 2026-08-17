using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Responses
{
    public class ReminderRunResponseDto
    {
        public Guid Id { get; set; }
        public Guid ReminderId { get; set; }
        public DateTime ScheduledFor { get; set; }
        public DateTime? SentAt { get; set; }

        /// <summary>When the user confirmed the occurrence.</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>When the task was actually carried out, as reported by the user.</summary>
        public DateTime? PerformedAt { get; set; }

        /// <summary>Type of the rule when the occurrence was created, not its type today.</summary>
        public ReminderType? Type { get; set; }

        public string? Note { get; set; }
        public ReminderRunStatus Status { get; set; }
        public string? Channel { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
