using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.NutritionModule.Mapper
{
    public static class NutritionGoalMapper
    {
        public static NutritionGoal ToEntity(UpsertNutritionGoalDto dto, Guid petId) => new()
        {
            PetId = petId,
            DailyCalorieTarget = dto.DailyCalorieTarget,
            DailyPortionTarget = dto.DailyPortionTarget,
            PortionUnit = dto.PortionUnit,
            MealsPerDay = dto.MealsPerDay,
            CreatedAt = DateTime.UtcNow
        };

        public static void ApplyUpsert(this NutritionGoal goal, UpsertNutritionGoalDto dto)
        {
            goal.DailyCalorieTarget = dto.DailyCalorieTarget;
            goal.DailyPortionTarget = dto.DailyPortionTarget;
            goal.PortionUnit = dto.PortionUnit;
            goal.MealsPerDay = dto.MealsPerDay;
            goal.UpdatedAt = DateTime.UtcNow;
        }

        public static void PatchEntity(this NutritionGoal goal, PatchNutritionGoalDto dto)
        {
            if (dto.DailyCalorieTarget.IsSet) goal.DailyCalorieTarget = dto.DailyCalorieTarget.Value;
            if (dto.DailyPortionTarget.IsSet) goal.DailyPortionTarget = dto.DailyPortionTarget.Value;
            if (dto.PortionUnit.IsSet) goal.PortionUnit = dto.PortionUnit.Value;
            if (dto.MealsPerDay.IsSet) goal.MealsPerDay = dto.MealsPerDay.Value;
            goal.UpdatedAt = DateTime.UtcNow;
        }

        public static NutritionGoalResponseDto ToDto(this NutritionGoal goal) => new()
        {
            Id = goal.Id,
            PetId = goal.PetId,
            DailyCalorieTarget = goal.DailyCalorieTarget,
            DailyPortionTarget = goal.DailyPortionTarget,
            PortionUnit = goal.PortionUnit,
            MealsPerDay = goal.MealsPerDay,
            CreatedAt = goal.CreatedAt,
            UpdatedAt = goal.UpdatedAt
        };
    }
}
