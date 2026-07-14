using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests
{
    public class CreatePetWeightLogDto
    {
        public decimal WeightKg { get; set; }
        [Required]
        public DateTime? MeasuredAt { get; set; }
        public string? Notes { get; set; }
    }
}
