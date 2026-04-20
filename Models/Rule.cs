using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Models
{
    public class Rule
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Version { get; set; } = null!;
        public string ConditionExpr { get; set; } = null!;
        public string RecommendationTemplate { get; set; } = null!;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
    }
}