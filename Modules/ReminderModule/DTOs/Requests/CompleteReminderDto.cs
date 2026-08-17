namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Requests
{
    /// <summary>
    /// Payload of the quick log window. The type is not here on purpose — it comes from the
    /// rule and is not the user's to change at completion time.
    /// </summary>
    public class CompleteReminderDto
    {
        /// <summary>
        /// When the task was actually carried out. Defaults to now; may be backdated when the
        /// user confirms late, or set ahead of the notification when they confirm early.
        /// </summary>
        public DateTime? PerformedAt { get; set; }

        public string? Note { get; set; }

        /// <summary>Only used for types that file a health record.</summary>
        public string? Dosage { get; set; }

        /// <summary>Only used for types that file a health record.</summary>
        public string? Provider { get; set; }
    }
}
