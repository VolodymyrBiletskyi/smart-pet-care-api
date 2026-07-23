using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class JournalEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public JournalEntryType Type { get; set; }

        public string Title { get; set; } = null!;
        public string? Notes { get; set; }

        public JournalEntrySeverity? Severity { get; set; }

        public DateTime ObservedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
