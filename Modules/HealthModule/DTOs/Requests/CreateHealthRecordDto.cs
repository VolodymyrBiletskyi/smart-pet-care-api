using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.DTOs.Requests
{
    public class CreateHealthRecordDto
    {
        public HealthRecordType Type { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public List<SymptomType>? Symptoms { get; set; }
        public DateTime PerformedAt { get; set; }
        public DateTime? NextDueAt { get; set; }
        public string? Dosage { get; set; }
        public string? Provider { get; set; }
    }
}
