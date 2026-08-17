

using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class ReminderRun
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ReminderId { get; set; }

        public DateTime ScheduledFor { get; set; }
        public DateTime? SentAt { get; set; }

        /// <summary>When the user confirmed the occurrence.</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// When the task was actually carried out, which the user can correct in the log
        /// window. Kept apart from <see cref="CompletedAt"/> because a correction may point
        /// before the notification was sent, and because every recalculation counts from here.
        /// </summary>
        public DateTime? PerformedAt { get; set; }

        /// <summary>
        /// Type of the rule at the time the occurrence was created. Snapshotted so that
        /// editing a reminder later cannot silently recategorise finished history.
        /// Null for runs created before this column existed.
        /// </summary>
        public ReminderType? Type { get; set; }

        public string? Note { get; set; }

        public ReminderRunStatus Status { get; set; } = ReminderRunStatus.Pending;
        public string? Channel { get; set; }
        public string DeliveryMeta { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

    }
}