using smart_pet_care_api.Common.Patching;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.DTOs.Requests
{
    /// <summary>
    /// A correction to a session already logged. Every field is optional and a field left out
    /// of the body is left alone, so sending <c>null</c> is how a value is cleared.
    /// <c>Source</c> is deliberately absent: it records which provider produced the row, and a
    /// hand-typed note does not become a collar reading because someone edited it.
    /// </summary>
    public class PatchActivityLogDto
    {
        public PatchField<DateTime> RecordedAt { get; set; }
        public PatchField<int?> Steps { get; set; }
        public PatchField<ActivityType?> Type { get; set; }

        /// <summary>
        /// Clearing this while a duration remains does not leave the session unweighted — an
        /// intensity is derived from the type again, exactly as on create.
        /// </summary>
        public PatchField<ActivityIntensity?> Intensity { get; set; }

        public PatchField<int?> DurationMinutes { get; set; }
        public PatchField<string?> Location { get; set; }
        public PatchField<string?> Note { get; set; }
    }
}
