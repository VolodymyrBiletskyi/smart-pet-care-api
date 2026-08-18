using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests
{
    public class CreatePetWeightLogDto
    {
        /// <summary>
        /// Weighing reminder this measurement answers. Supplying it closes the pending
        /// occurrence and moves the reminder on from <see cref="MeasuredAt"/>.
        /// </summary>
        public Guid? ReminderId { get; set; }

        public decimal WeightKg { get; set; }
        [Required]
        public DateTime? MeasuredAt { get; set; }
        public string? Notes { get; set; }
    }
}
