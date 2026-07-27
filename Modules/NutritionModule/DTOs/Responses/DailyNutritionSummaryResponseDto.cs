using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.DTOs.Responses
{
    public class DailyNutritionSummaryResponseDto
    {
        public Guid PetId { get; set; }
        public DateOnly Date { get; set; }
        public int UtcOffsetMinutes { get; set; }

        public int MealCount { get; set; }
        public int TotalCalories { get; set; }
        public List<PortionTotalDto> PortionTotalsByUnit { get; set; } = new();

        /// <summary>Number of active <c>Feeding</c> reminders scheduled for this pet.</summary>
        public int ScheduledFeedings { get; set; }

        /// <summary>The pet's current goal, or null if none is set.</summary>
        public NutritionGoalResponseDto? Goal { get; set; }

        /// <summary>Goal-vs-actual comparison, present only when a goal is set.</summary>
        public NutritionComparisonDto? Comparison { get; set; }
    }

    public class PortionTotalDto
    {
        public PortionUnit Unit { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class NutritionComparisonDto
    {
        public int? CaloriesRemaining { get; set; }
        public bool? CalorieTargetMet { get; set; }

        public int? MealsRemaining { get; set; }
        public bool? MealsTargetMet { get; set; }

        public decimal? PortionRemaining { get; set; }
        public bool? PortionTargetMet { get; set; }
    }
}
