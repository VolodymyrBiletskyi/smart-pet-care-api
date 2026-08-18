using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class FeedingLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        /// <summary>
        /// Reminder this feeding answers, when it was logged against one. Nulled rather than
        /// cascaded when the reminder goes away. Null for feedings logged on their own.
        /// </summary>
        public Guid? ReminderId { get; set; }

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