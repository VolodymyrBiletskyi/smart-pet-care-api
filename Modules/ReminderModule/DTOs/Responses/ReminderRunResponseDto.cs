using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.DTOs.Responses
{
    public class ReminderRunResponseDto
    {
        public Guid Id { get; set; }
        public Guid ReminderId { get; set; }
        public DateTime ScheduledFor { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public ReminderRunStatus Status { get; set; }
        public string? Channel { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
