using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    /// <summary>
    /// A single activity note: one walk, one play session, one measurement.
    /// Distinct from <see cref="ActivityDaily"/>, which is a per-day aggregate keyed by
    /// source and shaped for a device feed.
    /// </summary>
    public class ActivityLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public DateTime RecordedAt { get; set; }

        public int? Steps { get; set; }

        /// <summary>What the session was. Null on rows written before types existed.</summary>
        public ActivityType? Type { get; set; }

        /// <summary>
        /// Filled in from <see cref="Type"/> when the caller left it out and there is a
        /// duration to weight; see <c>ActivityLogService</c>.
        /// </summary>
        public ActivityIntensity? Intensity { get; set; }

        public int? DurationMinutes { get; set; }

        public string? Location { get; set; }

        public string? Note { get; set; }

        /// <summary>Which provider produced the reading. Manual until a device is wired up.</summary>
        public ActivitySource Source { get; set; } = ActivitySource.Manual;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
