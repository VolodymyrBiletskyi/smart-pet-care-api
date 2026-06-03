using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Requests
{
    public class CreateReminderDto
    {
        public Guid PetId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public ReminderType Type { get; set; }
        public DaysOfWeek[] Days { get; set; } = [];
        public bool IsRepeatable { get; set; }
        public TimeOnly Time { get; set; }
        public DateTime? EndAt { get; set; }
    }
}
