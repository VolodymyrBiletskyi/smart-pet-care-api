namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Responses
{
    public class ReminderCompletionResponseDto
    {
        public ReminderRunResponseDto Run { get; set; } = null!;

        /// <summary>The rule with its recalculated next trigger.</summary>
        public ReminderResponseDto Reminder { get; set; } = null!;

        /// <summary>Set when the completion filed a health record for a medical type.</summary>
        public Guid? HealthRecordId { get; set; }

        /// <summary>
        /// True when this completion had already been registered — the client sent it twice
        /// and the schedule was left alone.
        /// </summary>
        public bool AlreadyRecorded { get; set; }
    }
}
