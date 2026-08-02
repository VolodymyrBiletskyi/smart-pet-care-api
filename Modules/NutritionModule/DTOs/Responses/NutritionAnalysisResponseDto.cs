using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.DTOs.Responses
{
    public class NutritionAnalysisResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }

        /// <summary>The local day that was analysed.</summary>
        public DateOnly Date { get; set; }
        public int UtcOffsetMinutes { get; set; }

        /// <summary>How the day's calories compared with the pet's target.</summary>
        public FeedingStatus Status { get; set; }

        /// <summary>
        /// The daily calorie need this day was graded against: the pet's
        /// nutrition goal when it sets a target above zero, otherwise the
        /// figure the AI derived from the pet's body data.
        /// </summary>
        public decimal TargetCalories { get; set; }
        public decimal ActualCalories { get; set; }

        /// <summary>Signed percentage away from <see cref="TargetCalories"/>.</summary>
        public decimal DeviationPct { get; set; }

        public string Disclaimer { get; set; } = null!;

        /// <summary>Meals the analysis was based on, as they stood when it ran.</summary>
        public int MealCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// The two most recent analyses for a pet. Both are null when none has been
    /// requested yet; <c>Previous</c> is null after the first one.
    /// </summary>
    public class NutritionAnalysisHistoryResponseDto
    {
        public NutritionAnalysisResponseDto? Latest { get; set; }
        public NutritionAnalysisResponseDto? Previous { get; set; }
    }
}
