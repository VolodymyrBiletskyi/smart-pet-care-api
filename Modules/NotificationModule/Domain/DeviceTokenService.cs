using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NotificationModule.DTOs.Requests;
using smart_pet_care_api.Modules.NotificationModule.Repository;

namespace smart_pet_care_api.Modules.NotificationModule.Domain
{
    public class DeviceTokenService : IDeviceTokenService
    {
        private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(60);

        private readonly IDeviceTokenRepository _repo;

        public DeviceTokenService(IDeviceTokenRepository repo)
        {
            _repo = repo;
        }

        public async Task RegisterAsync(Guid userId, RegisterDeviceTokenDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Token))
                throw new ArgumentException("Device token is required");

            var token = dto.Token.Trim();

            await _repo.RemoveStaleAsync(DateTime.UtcNow - StaleAfter);

            var existing = await _repo.GetByUserAndTokenAsync(userId, token);
            if (existing is not null)
            {
                existing.LastSeenAt = DateTime.UtcNow;
                existing.Platform = dto.Platform;
            }
            else
            {
                await _repo.AddAsync(new DeviceToken
                {
                    UserId = userId,
                    Token = token,
                    Platform = dto.Platform,
                    CreatedAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                });
            }

            await _repo.SaveChangesAsync();
        }

        public async Task<bool> UnregisterAsync(Guid userId, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var removed = await _repo.RemoveByUserAndTokenAsync(userId, token.Trim());
            if (removed)
                await _repo.SaveChangesAsync();

            return removed;
        }
    }
}
