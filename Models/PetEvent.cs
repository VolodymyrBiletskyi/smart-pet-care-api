using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class PetEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public PetEventType Type { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public DateTime ScheduledAt { get; set; }
        public DateTime? EndAt { get; set; }

        public PetEventStatus Status { get; set; } = PetEventStatus.Planned;
        public PetEventPriority Priority { get; set; } = PetEventPriority.Normal;

        public bool IsSystemGenerated { get; set; } = false;

        public SourceType SourceType { get; set; } = SourceType.Manual;
        public Guid? SourceId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}