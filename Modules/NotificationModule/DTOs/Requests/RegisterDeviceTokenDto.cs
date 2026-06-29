using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NotificationModule.DTOs.Requests
{
    public class RegisterDeviceTokenDto
    {
        public string Token { get; set; } = null!;
        public DevicePlatform Platform { get; set; }
    }
}
