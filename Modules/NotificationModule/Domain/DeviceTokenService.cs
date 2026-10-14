using smart_pet_care_api.Common.Api;
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
                throw new ValidationException(ErrorCodes.Notification.DeviceTokenRequired, "Device token is required");

            var token = dto.Token.Trim();
            var now = DateTime.UtcNow;

            await _repo.RemoveStaleAsync(now - StaleAfter);

            await _repo.UpsertAsync(new DeviceToken
            {
                UserId = userId,
                Token = token,
                Platform = dto.Platform,
                CreatedAt = now,
                LastSeenAt = now
            });
        }

        public async Task<bool> UnregisterAsync(Guid userId, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            // No SaveChanges: the delete is its own statement. Saving here would flush
            // whatever else the request-scoped context happens to be tracking.
            return await _repo.RemoveByUserAndTokenAsync(userId, token.Trim());
        }
    }
}

