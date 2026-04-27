using System;
using System.Collections.Generic;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class AiSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        public AiSessionType Type { get; set; }
        public string? Title { get; set; }

        public AiSessionStatus Status { get; set; } = AiSessionStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
    }
}