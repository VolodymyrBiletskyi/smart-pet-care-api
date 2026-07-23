using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.JournalModule.DTOs.Requests
{
    public class CreateJournalEntryDto
    {
        public JournalEntryType Type { get; set; }
        public string Title { get; set; } = null!;
        public string? Notes { get; set; }
        public JournalEntrySeverity? Severity { get; set; }
        public DateTime ObservedAt { get; set; }
    }
}
