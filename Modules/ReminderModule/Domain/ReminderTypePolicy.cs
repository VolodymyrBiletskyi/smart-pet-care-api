using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    /// <summary>
    /// Where a reminder type's log belongs and how strictly its interval must be honoured.
    /// </summary>
    public static class ReminderTypePolicy
    {
        /// <summary>
        /// Health record a completed occurrence should be filed as, or null when no health
        /// record describes it: grooming's log is the closed run, while weighing and feeding
        /// have their own optional logs in PetWeightHistory and FeedingModule.
        ///
        /// The two enums are deliberately not merged: ReminderType carries Feeding, Activity
        /// and the grooming labels that no health record describes, while HealthRecordType
        /// carries Surgery, Symptom and HealthNote that no reminder produces. ParasiteTreatment
        /// and AntiParasiteTreatment differ in name only — renaming either would break the
        /// wire contract for no functional gain, so the mismatch is resolved here instead.
        /// </summary>
        public static HealthRecordType? ToHealthRecordType(ReminderType type) => type switch
        {
            ReminderType.Vaccination => HealthRecordType.Vaccination,
            ReminderType.ParasiteTreatment => HealthRecordType.AntiParasiteTreatment,
            ReminderType.Deworming => HealthRecordType.Deworming,
            ReminderType.VetVisit => HealthRecordType.VetVisit,

            // Medication is intentionally absent: a twice-daily pill would file a record per
            // dose. It stays a calendar rule and a record is only created if the user enters
            // one by hand.
            _ => null
        };

        /// <summary>
        /// Types whose interval is a safety property, so the recalculation mode is not the
        /// client's to choose. Antiparasitics protect for a fixed number of days from the
        /// dose; a calendar rule or a weekday nudge would quietly shorten that cover.
        /// </summary>
        public static bool RequiresCompletionDrivenRecalc(ReminderType type) => type
            is ReminderType.Vaccination
            or ReminderType.ParasiteTreatment
            or ReminderType.Deworming
            or ReminderType.VetVisit;
    }
}
