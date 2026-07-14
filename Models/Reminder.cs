using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class Reminder
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PetId { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public ReminderType Type { get; set; } = ReminderType.Feeding;
        public ReminderStatus Status { get; set; } = ReminderStatus.Active;

        public RepeatType RepeatType { get; set; } = RepeatType.Weekly;

        public DaysOfWeek[] Days { get; set; } = [];

        public DateOnly? Date { get; set; }

        public TimeSpan TimeOfDay { get; set; }

        public int UtcOffsetMinutes { get; set; }

        public DateTime StartAt { get; set; }
        public DateTime? NextTriggerAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool IsSystemGenerated { get; set; } = false;
        public SourceType SourceType { get; set; }
        public Guid? SourceId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Pet? Pet { get; set; }
        public ICollection<ReminderRun> ReminderRuns { get; set; } = new List<ReminderRun>();
    }
}
