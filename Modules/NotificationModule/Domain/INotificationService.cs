using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NotificationModule.Domain
{
    public interface INotificationService
    {
        Task<bool> SendReminderNotificationAsync(Reminder reminder, DateTime scheduledFor, CancellationToken ct);
    }
}
