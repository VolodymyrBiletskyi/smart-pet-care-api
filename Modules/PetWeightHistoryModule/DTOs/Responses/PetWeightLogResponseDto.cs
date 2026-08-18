namespace smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Responses
{
    public class PetWeightLogResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }

        /// <summary>Reminder this measurement was logged against, if any.</summary>
        public Guid? ReminderId { get; set; }

        public decimal WeightKg { get; set; }
        public DateTime MeasuredAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
