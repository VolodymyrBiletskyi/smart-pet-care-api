using System.ComponentModel.DataAnnotations;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.FeedingModule.DTOs.Requests
{
    public class CreateFeedingLogDto
    {
        /// <summary>
        /// Feeding reminder this entry answers. Supplying it closes the pending occurrence
        /// and moves the reminder on from <see cref="FedAt"/>.
        /// </summary>
        public Guid? ReminderId { get; set; }

        [Required]
        public DateTime? FedAt { get; set; }
        public FoodType FoodType { get; set; }
        public string? FoodName { get; set; }
        public decimal? PortionAmount { get; set; }
        public PortionUnit? PortionUnit { get; set; }
        public int? ApproxCalories { get; set; }
        public string? Notes { get; set; }
    }
}
