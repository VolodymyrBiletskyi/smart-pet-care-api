using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Responses
{
    /// <summary>
    /// A completed occurrence in the pet's care history. Carries the rule's title and the
    /// type snapshotted on the run, so the entry stays readable and correctly categorised even
    /// if the rule is edited or deleted afterwards.
    /// </summary>
    public class ReminderRunHistoryDto
    {
        public Guid RunId { get; set; }
        public Guid ReminderId { get; set; }
        public Guid PetId { get; set; }
        public string Title { get; set; } = null!;
        public ReminderType? Type { get; set; }
        public DateTime ScheduledFor { get; set; }
        public DateTime? PerformedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public ReminderRunStatus Status { get; set; }
        public string? Note { get; set; }
    }
}
