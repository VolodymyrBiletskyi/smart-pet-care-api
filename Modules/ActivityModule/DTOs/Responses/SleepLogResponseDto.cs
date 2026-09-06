using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.DTOs.Responses
{
    public class SleepLogResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public DateTime SleepDate { get; set; }
        public decimal Hours { get; set; }
        public string? Note { get; set; }
        public ActivitySource Source { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Null until the log is edited.</summary>
        public DateTime? UpdatedAt { get; set; }
    }
}
