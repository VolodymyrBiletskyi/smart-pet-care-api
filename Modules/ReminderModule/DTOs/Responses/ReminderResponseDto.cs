using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Responses
{
    public class ReminderResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public AnimalSpecies? PetSpecies { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public ReminderType Type { get; set; }
        public ReminderStatus Status { get; set; }
        public RepeatType RepeatType { get; set; }

        /// <summary>Repeat every N days/weeks/months, depending on <see cref="RepeatType"/>.</summary>
        public int IntervalN { get; set; }

        public RecalcStrategy RecalcStrategy { get; set; }
        public DaysOfWeek[] Days { get; set; } = [];
        public DateOnly? Date { get; set; }
        public TimeSpan TimeOfDay { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime? NextTriggerAt { get; set; }
        public DateTime? EndAt { get; set; }

        /// <summary>Origin the interval is counted from; moves on completion-driven rules.</summary>
        public DateTime ScheduleAnchorAt { get; set; }

        /// <summary>When the oldest unconfirmed occurrence was due, null when nothing is pending.</summary>
        public DateTime? OverdueSince { get; set; }

        public DateTime? LastCompletedAt { get; set; }
        public bool IsSystemGenerated { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
