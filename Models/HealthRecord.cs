using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class HealthRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        /// <summary>
        /// Reminder this record answers, when it was logged against one. Nulled rather than
        /// cascaded when the reminder goes away — deleting a rule must not delete treatment
        /// history. Null for records entered by hand with no rule behind them.
        /// </summary>
        public Guid? ReminderId { get; set; }

        public HealthRecordType Type { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public List<SymptomType>? Symptoms { get; set; }

        public DateTime PerformedAt { get; set; }
        public DateTime? NextDueAt { get; set; }

        public string? Dosage { get; set; }
        public string? Provider { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
