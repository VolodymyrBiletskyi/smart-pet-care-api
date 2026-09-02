using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.DTOs.Responses
{
    public class ActivityLogResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public DateTime RecordedAt { get; set; }
        public int? Steps { get; set; }
        public ActivityType? Type { get; set; }
        public ActivityIntensity? Intensity { get; set; }
        public int? DurationMinutes { get; set; }

        /// <summary>
        /// Duration weighted by intensity — the figure the wellness score sums over a day.
        /// Derived on read rather than stored, so retuning the weights does not need a
        /// backfill. Null when the log carries no duration.
        /// </summary>
        public int? ActiveMinutes { get; set; }

        public string? Location { get; set; }
        public string? Note { get; set; }
        public ActivitySource Source { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
