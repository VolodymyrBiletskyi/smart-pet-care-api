using System.ComponentModel.DataAnnotations;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Requests
{
    public class CreateReminderDto
    {
        public Guid PetId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public ReminderType Type { get; set; }

        public RepeatType RepeatType { get; set; }

        /// <summary>
        /// Repeat every N units of <see cref="RepeatType"/>. Defaults to 1, so existing clients
        /// keep the behaviour they have. Yearly is Monthly with 12.
        /// </summary>
        public int IntervalN { get; set; } = 1;

        /// <summary>
        /// How the next date is derived after a completion. Ignored for medical types, which
        /// the server pins to <see cref="RecalcStrategy.FromCompletion"/>.
        /// </summary>
        public RecalcStrategy RecalcStrategy { get; set; } = RecalcStrategy.Calendar;

        /// <summary>
        /// Trigger days for Weekly. For other repeat types these are only the alignment target
        /// of <see cref="RecalcStrategy.FromCompletionAlignedToWeekday"/>.
        /// </summary>
        public DaysOfWeek[] Days { get; set; } = [];

        public DateOnly? Date { get; set; }

        public TimeOnly Time { get; set; }
        public DateTime? EndAt { get; set; }
        [Required]
        public int UtcOffsetMinutes { get; set; }
    }
}
