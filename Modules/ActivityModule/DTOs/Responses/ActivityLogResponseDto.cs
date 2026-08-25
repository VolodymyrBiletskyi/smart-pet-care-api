using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.DTOs.Responses
{
    public class ActivityLogResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public DateTime RecordedAt { get; set; }
        public int? Steps { get; set; }
        public string? Location { get; set; }
        public string? Note { get; set; }
        public ActivitySource Source { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
