using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class Recommendation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PetId { get; set; }
        public Pet Pet { get; set; } = null!;
        public Guid RuleId { get; set; }
        public Rule Rule { get; set; } = null!;

        public string? Severity { get; set; }
        public string Message { get; set; } = null!;
        public Status Status { get; set; } = Status.Open;
        public string EvidenceSnapshot { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;



    }
}