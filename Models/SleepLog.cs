using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    /// <summary>
    /// Hours slept on one day. Kept out of <see cref="ActivityLog"/> because sleep shares
    /// none of its shape — no steps, no place, no intensity — and because the wellness score
    /// reads it as a daily total rather than as a session. Several rows may share a date;
    /// the day is the sum of them, capped at 24 hours.
    /// </summary>
    public class SleepLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        /// <summary>The day the sleep is credited to, as UTC midnight.</summary>
        public DateTime SleepDate { get; set; }

        public decimal Hours { get; set; }

        public string? Note { get; set; }

        /// <summary>
        /// Manual until a collar reports sleep. A device feed writes
        /// <see cref="ActivityDaily.SleepHours"/> today, not this table.
        /// </summary>
        public ActivitySource Source { get; set; } = ActivitySource.Manual;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
