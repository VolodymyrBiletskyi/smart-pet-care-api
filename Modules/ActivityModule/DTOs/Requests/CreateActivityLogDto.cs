using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.DTOs.Requests
{
    public class CreateActivityLogDto
    {
        public DateTime RecordedAt { get; set; }
        public int? Steps { get; set; }
        public string? Location { get; set; }
        public string? Note { get; set; }

        /// <summary>
        /// What the pet did. Optional — a log can still be a bare step count or a note.
        /// Use <see cref="ActivityType.Other"/> plus <see cref="Note"/> for anything the
        /// list does not name.
        /// </summary>
        public ActivityType? Type { get; set; }

        /// <summary>
        /// How hard the session was. Left out, it is derived from <see cref="Type"/>
        /// whenever <see cref="DurationMinutes"/> is present.
        /// </summary>
        public ActivityIntensity? Intensity { get; set; }

        public int? DurationMinutes { get; set; }

        /// <summary>
        /// Where the numbers come from. Only <see cref="ActivitySource.Manual"/> has a
        /// provider today; anything else is rejected until its integration lands.
        /// </summary>
        public ActivitySource? Source { get; set; }
    }
}
