using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NutritionModule.Domain
{
    public interface INutritionAnalysisService
    {
        /// <summary>
        /// Builds the daily summary, asks the classifier to grade it, stores the
        /// result and drops anything older than the two most recent analyses.
        /// </summary>
        /// <param name="request">
        /// Optional per-field overrides. Anything omitted falls back to the
        /// pet's stored data and the analysed day's feeding logs, so a null
        /// request behaves exactly as before the body existed.
        /// </param>
        Task<NutritionAnalysisResponseDto> AnalyzeAsync(
            Guid petId,
            Guid userId,
            DateOnly? date,
            int utcOffsetMinutes,
            NutritionAnalysisRequestDto? request = null,
            CancellationToken cancellationToken = default);

        /// <summary>The stored latest analysis and the one before it.</summary>
        Task<NutritionAnalysisHistoryResponseDto> GetRecentAsync(Guid petId, Guid userId);
    }
}
