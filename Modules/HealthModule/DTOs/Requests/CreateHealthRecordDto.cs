using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.DTOs.Requests
{
    public class CreateHealthRecordDto
    {
        /// <summary>
        /// Reminder this record answers, when the user is logging against one. Supplying it
        /// moves the reminder on from <see cref="PerformedAt"/>, and NextDueAt is then written
        /// by the server so the record and the schedule cannot disagree.
        /// </summary>
        public Guid? ReminderId { get; set; }

        public HealthRecordType Type { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public List<SymptomType>? Symptoms { get; set; }
        public DateTime PerformedAt { get; set; }
        public DateTime? NextDueAt { get; set; }
        public string? Dosage { get; set; }
        public string? Provider { get; set; }
    }
}
