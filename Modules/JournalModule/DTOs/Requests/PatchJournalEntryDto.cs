using smart_pet_care_api.Common.Patching;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.JournalModule.DTOs.Requests
{
    public class PatchJournalEntryDto
    {
        public PatchField<JournalEntryType> Type { get; set; }
        public PatchField<string> Title { get; set; }
        public PatchField<string?> Notes { get; set; }
        public PatchField<List<SymptomType>?> Symptoms { get; set; }
        public PatchField<JournalEntrySeverity?> Severity { get; set; }
        public PatchField<DateTime> ObservedAt { get; set; }
    }
}
