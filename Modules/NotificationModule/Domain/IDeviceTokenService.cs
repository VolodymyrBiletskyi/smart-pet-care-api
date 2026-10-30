using smart_pet_care_api.Modules.NotificationModule.DTOs.Requests;

namespace smart_pet_care_api.Modules.NotificationModule.Domain
{
    public interface IDeviceTokenService
    {
        Task RegisterAsync(Guid userId, RegisterDeviceTokenDto dto);
        Task<bool> UnregisterAsync(Guid userId, string token);

        /// <summary>
        /// Drops every registration this account has, for the one event where leaving
        /// any of them is the whole problem — a password reset. Logout unregisters only
        /// the device signing out, because the user's other devices are still theirs;
        /// after a reset that assumption is exactly what no longer holds.
        /// </summary>
        Task<int> UnregisterAllAsync(Guid userId);
    }
}
