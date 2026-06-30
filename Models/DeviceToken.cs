using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class DeviceToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }

        public string Token { get; set; } = null!;
        public DevicePlatform Platform { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
