using smart_pet_care_api.Common.Patching;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.DTOs.Requests
{
    public class PatchHealthRecordDto
    {
        public PatchField<HealthRecordType> Type { get; set; }
        public PatchField<string> Title { get; set; }
        public PatchField<string?> Description { get; set; }
        public PatchField<List<SymptomType>?> Symptoms { get; set; }
        public PatchField<DateTime> PerformedAt { get; set; }
        public PatchField<DateTime?> NextDueAt { get; set; }
        public PatchField<string?> Dosage { get; set; }
        public PatchField<string?> Provider { get; set; }
    }
}
