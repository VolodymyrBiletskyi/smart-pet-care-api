using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NotificationModule.Repository
{
    public interface IDeviceTokenRepository
    {
        Task<DeviceToken?> GetByUserAndTokenAsync(Guid userId, string token);
        Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId);
        Task AddAsync(DeviceToken token);
        Task<bool> RemoveByUserAndTokenAsync(Guid userId, string token);
        Task RemoveByTokensAsync(IEnumerable<string> tokens);
        Task<int> RemoveStaleAsync(DateTime olderThan);
        Task<int> SaveChangesAsync();
    }
}
