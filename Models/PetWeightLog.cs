using System;

namespace smart_pet_care_api.Models
{
    public class PetWeightLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public decimal WeightKg { get; set; }

        public DateTime MeasuredAt { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
