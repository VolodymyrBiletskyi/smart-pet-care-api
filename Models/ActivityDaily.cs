using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class ActivityDaily
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public DateTime ActivityDate { get; set; }

        public int? Steps { get; set; }
        public decimal? ActiveMinutes { get; set; }
        public decimal? SleepHours { get; set; }

        public ActivitySource Source { get; set; } = ActivitySource.Mock;

        public string RawPayload { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}