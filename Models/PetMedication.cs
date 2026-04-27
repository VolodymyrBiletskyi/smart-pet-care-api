using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static smart_pet_care_api.Models.Enums;
using static smart_pet_care_api.Models.Reminder;

namespace smart_pet_care_api.Models
{
    public class PetMedication
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PetId { get; set; }

        public string Name { get; set; } = null!;
        public string? Dosage { get; set; }
        public string? Instructions { get; set; }

        public Frequency MedicationFrequency { get; set; } = Frequency.Daily;
        public int Interval { get; set; } = 1;

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

    }
}