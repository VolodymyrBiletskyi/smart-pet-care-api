using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.DTOs.Requests
{
    public class UpsertNutritionGoalDto
    {
        public int? DailyCalorieTarget { get; set; }
        public decimal? DailyPortionTarget { get; set; }
        public PortionUnit? PortionUnit { get; set; }
        public int? MealsPerDay { get; set; }
    }
}
