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

        /// <summary>
        /// Repeat every N units of <see cref="RepeatType"/>: days for Daily, weeks for
        /// Weekly, months for Monthly. 1 reproduces the original behaviour, so Yearly is
        /// Monthly with 12 (which also clamps 29 February the way Monthly already does).
        /// </summary>
        public int IntervalN { get; set; } = 1;

        public RecalcStrategy RecalcStrategy { get; set; } = RecalcStrategy.Calendar;

        /// <summary>
        /// Selected weekdays in the user's local time zone. For Weekly these are the
        /// trigger days; for the other repeat types they are only the alignment target of
        /// <see cref="RecalcStrategy.FromCompletionAlignedToWeekday"/>.
        /// </summary>
        public DaysOfWeek[] Days { get; set; } = [];

        public DateOnly? Date { get; set; }

        public TimeSpan TimeOfDay { get; set; }

        public int UtcOffsetMinutes { get; set; }

        public DateTime StartAt { get; set; }
        public DateTime? NextTriggerAt { get; set; }
        public DateTime? EndAt { get; set; }

        /// <summary>
        /// Origin every interval is counted from. Starts as <see cref="StartAt"/> and is
        /// rewritten on completion for the FromCompletion strategies; counting week parity
        /// from StartAt while counting the interval from the completion date would make the
        /// two disagree after the first late completion.
        /// </summary>
        public DateTime ScheduleAnchorAt { get; set; }

        /// <summary>
        /// When the oldest unconfirmed occurrence was due, or null when nothing is pending.
        /// Set when an occurrence fires, cleared on completion — this is what makes "three
        /// days without protection" visible instead of the reminder quietly rolling on.
        /// </summary>
        public DateTime? OverdueSince { get; set; }

        /// <summary>Date the user reported the task as actually done, not when they tapped.</summary>
        public DateTime? LastCompletedAt { get; set; }

        public bool IsSystemGenerated { get; set; } = false;
        public SourceType SourceType { get; set; }
        public Guid? SourceId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Pet? Pet { get; set; }
        public ICollection<ReminderRun> ReminderRuns { get; set; } = new List<ReminderRun>();
    }
}
