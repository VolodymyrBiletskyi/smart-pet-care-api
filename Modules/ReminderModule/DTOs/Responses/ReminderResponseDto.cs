using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Responses
{
    public class ReminderResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public ReminderType Type { get; set; }
        public ReminderStatus Status { get; set; }
        public DaysOfWeek[] Days { get; set; } = [];
        public bool IsRepeatable { get; set; }
        public TimeSpan TimeOfDay { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime? NextTriggerAt { get; set; }
        public DateTime? EndAt { get; set; }
        public bool IsSystemGenerated { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
