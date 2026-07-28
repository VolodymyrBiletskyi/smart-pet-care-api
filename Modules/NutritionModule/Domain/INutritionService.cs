using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NutritionModule.Domain
{
    public interface INutritionService
    {
        Task<NutritionGoalResponseDto?> GetGoalAsync(Guid petId, Guid userId);
        Task<NutritionGoalResponseDto> UpsertGoalAsync(Guid petId, Guid userId, UpsertNutritionGoalDto dto);
        Task<NutritionGoalResponseDto> PatchGoalAsync(Guid petId, Guid userId, PatchNutritionGoalDto dto);
        Task<bool> DeleteGoalAsync(Guid petId, Guid userId);

        Task<DailyNutritionSummaryResponseDto> GetDailySummaryAsync(
            Guid petId, Guid userId, DateOnly? date, int utcOffsetMinutes);
    }
}
