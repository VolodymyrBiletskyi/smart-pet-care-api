using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class FeedingLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }
        public Pet Pet { get; set; } = null!;

        public DateTime FedAt { get; set; }

        public FoodType FoodType { get; set; }
        public string? FoodName { get; set; }

        public decimal? PortionAmount { get; set; }
        public PortionUnit? PortionUnit { get; set; }

        public int? ApproxCalories { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}