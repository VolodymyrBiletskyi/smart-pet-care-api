using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Requests
{
    public class PatchReminderDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DaysOfWeek[]? Days { get; set; }
        public bool? IsRepeatable { get; set; }
        public TimeOnly? Time { get; set; }
        public DateTime? EndAt { get; set; }
        public ReminderStatus? Status { get; set; }
        public int? UtcOffsetMinutes { get; set; }
    }
}
