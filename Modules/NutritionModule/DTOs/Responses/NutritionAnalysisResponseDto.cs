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

        public NutritionGrade Grade { get; set; }

        /// <summary>Overall quality of the day, from 0 to 100.</summary>
        public int Score { get; set; }

        public string Summary { get; set; } = null!;
        public List<string> Advice { get; set; } = new();
        public string Disclaimer { get; set; } = null!;

        /// <summary>Figures the analysis was based on, as they stood when it ran.</summary>
        public int MealCount { get; set; }
        public int TotalCalories { get; set; }

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
