using System;

namespace smart_pet_care_api.Models
{
    public class PetWeightLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        /// <summary>
        /// Reminder this measurement answers, when it was logged against one. Nulled rather
        /// than cascaded when the reminder goes away — deleting a rule must not delete weight
        /// history. Null for measurements entered with no rule behind them.
        /// </summary>
        public Guid? ReminderId { get; set; }

        public decimal WeightKg { get; set; }

        public DateTime MeasuredAt { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
