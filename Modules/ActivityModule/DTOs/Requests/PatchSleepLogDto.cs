using smart_pet_care_api.Common.Patching;

namespace smart_pet_care_api.Modules.ActivityModule.DTOs.Requests
{
    /// <summary>
    /// A correction to a night or a nap already logged. Fields left out of the body are left
    /// alone; <c>Source</c> is absent for the same reason as on the activity patch.
    /// </summary>
    public class PatchSleepLogDto
    {
        /// <summary>
        /// Moving a row to another day re-checks that day's 24-hour total, not the old one's.
        /// </summary>
        public PatchField<DateTime> SleepDate { get; set; }

        public PatchField<decimal> Hours { get; set; }
        public PatchField<string?> Note { get; set; }
    }
}
