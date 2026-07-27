using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class NutritionGoal
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public int? DailyCalorieTarget { get; set; }

        public decimal? DailyPortionTarget { get; set; }
        public PortionUnit? PortionUnit { get; set; }

        public int? MealsPerDay { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
