using smart_pet_care_api.Modules.NotificationModule.DTOs.Requests;

namespace smart_pet_care_api.Modules.NotificationModule.Domain
{
    public interface IDeviceTokenService
    {
        Task RegisterAsync(Guid userId, RegisterDeviceTokenDto dto);
        Task<bool> UnregisterAsync(Guid userId, string token);
    }
}
