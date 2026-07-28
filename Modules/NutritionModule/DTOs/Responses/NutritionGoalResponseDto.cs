using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.DTOs.Responses
{
    public class NutritionGoalResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public int? DailyCalorieTarget { get; set; }
        public decimal? DailyPortionTarget { get; set; }
        public PortionUnit? PortionUnit { get; set; }
        public int? MealsPerDay { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
