

using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class ReminderRun
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ReminderId { get; set; }

        public DateTime ScheduledFor { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public ReminderRunStatus Status { get; set; } = ReminderRunStatus.Pending;
        public string? Channel { get; set; }
        public string DeliveryMeta { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

    }
}