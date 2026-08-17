using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Responses
{
    /// <summary>
    /// One occurrence of a rule. Past occurrences are backed by a stored run; future ones are
    /// projected from the repeat rule and exist only in this response, so that editing a
    /// routine does not mean deleting and regenerating everything ahead of it.
    /// </summary>
    public class ReminderOccurrenceDto
    {
        public Guid ReminderId { get; set; }
        public Guid PetId { get; set; }
        public string Title { get; set; } = null!;
        public ReminderType Type { get; set; }
        public DateTime ScheduledFor { get; set; }

        /// <summary>Null for projected occurrences; set once a run exists.</summary>
        public Guid? RunId { get; set; }

        /// <summary>Null for projected occurrences.</summary>
        public ReminderRunStatus? Status { get; set; }

        public DateTime? CompletedAt { get; set; }
        public DateTime? PerformedAt { get; set; }

        /// <summary>False while the occurrence is only a projection.</summary>
        public bool IsMaterialized { get; set; }

        /// <summary>The occurrence is due and still unconfirmed.</summary>
        public bool IsOverdue { get; set; }
    }
}
