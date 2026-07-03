using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.FeedingModule.DTOs.Responses
{
    public class FeedingLogResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public DateTime FedAt { get; set; }
        public FoodType FoodType { get; set; }
        public string? FoodName { get; set; }
        public decimal? PortionAmount { get; set; }
        public PortionUnit? PortionUnit { get; set; }
        public int? ApproxCalories { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
