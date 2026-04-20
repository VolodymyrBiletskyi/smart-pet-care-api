using System;

namespace smart_pet_care_api.Models
{
    public class WellnessScore
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }
        public Pet Pet { get; set; } = null!;

        public DateTime ScoreDate { get; set; }

        public decimal Score { get; set; }

        public string FactorBreakdown { get; set; } = "{}";

        public string FormulaVersion { get; set; } = "v1";

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }
}