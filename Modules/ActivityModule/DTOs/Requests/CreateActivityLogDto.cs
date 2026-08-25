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
        /// Where the numbers come from. Only <see cref="ActivitySource.Manual"/> has a
        /// provider today; anything else is rejected until its integration lands.
        /// </summary>
        public ActivitySource? Source { get; set; }
    }
}
