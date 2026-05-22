using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Requests
{
    public class PatchReminderDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DayOfWeek[]? Days { get; set; }
        public bool? IsRepeatable { get; set; }
        public DateTime? Time { get; set; }
        public DateTime? EndAt { get; set; }
        public ReminderStatus? Status { get; set; }
    }
}
