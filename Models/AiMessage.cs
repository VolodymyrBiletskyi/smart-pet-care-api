using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class AiMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SessionId { get; set; }
        public AiSession Session { get; set; } = null!;

        public AiMessageRole Role { get; set; }
        public string Content { get; set; } = null!;

        public string? Metadata { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}