

using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class PetCondition
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PetId { get; set; }

        public bool IsActive { get; set; } = true;
        public ConditionType Type { get; set; }
        public string Name { get; set; } = null!;
        public string? Allergen { get; set; }
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}