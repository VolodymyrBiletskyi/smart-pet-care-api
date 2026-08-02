using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.Mapper
{
    public static class NutritionAnalysisMapper
    {
        public static NutritionAnalysisResponseDto ToDto(this NutritionAnalysis analysis) => new()
        {
            Id = analysis.Id,
            PetId = analysis.PetId,
            Date = analysis.Date,
            UtcOffsetMinutes = analysis.UtcOffsetMinutes,
            Status = analysis.Status,
            TargetCalories = analysis.TargetCalories,
            ActualCalories = analysis.ActualCalories,
            DeviationPct = analysis.DeviationPct,
            Disclaimer = analysis.Disclaimer,
            MealCount = analysis.MealCount,
            CreatedAt = analysis.CreatedAt
        };

        public static NutritionAnalysis ToEntity(
            ClassifierFeedingSummaryResult result,
            string disclaimer,
            Guid petId,
            DateOnly date,
            int utcOffsetMinutes,
            int mealCount) => new()
            {
                PetId = petId,
                Date = date,
                UtcOffsetMinutes = utcOffsetMinutes,
                Status = ToFeedingStatus(result.Status),
                TargetCalories = result.TargetCalories,
                ActualCalories = result.ActualCalories,
                DeviationPct = result.DeviationPct,
                Disclaimer = disclaimer,
                MealCount = mealCount,
                CreatedAt = DateTime.UtcNow
            };

        public static FeedingStatus ToFeedingStatus(ClassifierFeedingStatus status) => status switch
        {
            ClassifierFeedingStatus.ExtremeUnderTarget => FeedingStatus.ExtremeUnderTarget,
            ClassifierFeedingStatus.UnderTarget => FeedingStatus.UnderTarget,
            ClassifierFeedingStatus.OnTarget => FeedingStatus.OnTarget,
            ClassifierFeedingStatus.OverTarget => FeedingStatus.OverTarget,
            _ => FeedingStatus.ExtremeOverTarget
        };
    }
}
