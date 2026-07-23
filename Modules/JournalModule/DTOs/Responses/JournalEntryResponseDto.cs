using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.JournalModule.DTOs.Responses
{
    public class JournalEntryResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public JournalEntryType Type { get; set; }
        public string Title { get; set; } = null!;
        public string? Notes { get; set; }
        public JournalEntrySeverity? Severity { get; set; }
        public DateTime ObservedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
