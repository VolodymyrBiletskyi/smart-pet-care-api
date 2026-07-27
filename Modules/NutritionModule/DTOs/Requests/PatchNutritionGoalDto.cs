using smart_pet_care_api.Common.Patching;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.DTOs.Requests
{
    public class PatchNutritionGoalDto
    {
        public PatchField<int?> DailyCalorieTarget { get; set; }
        public PatchField<decimal?> DailyPortionTarget { get; set; }
        public PatchField<PortionUnit?> PortionUnit { get; set; }
        public PatchField<int?> MealsPerDay { get; set; }
    }
}
