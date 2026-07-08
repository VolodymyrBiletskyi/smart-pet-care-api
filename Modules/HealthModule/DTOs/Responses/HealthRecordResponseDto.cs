using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.DTOs.Responses
{
    public class HealthRecordResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public HealthRecordType Type { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime PerformedAt { get; set; }
        public DateTime? NextDueAt { get; set; }
        public string? Dosage { get; set; }
        public string? Provider { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
