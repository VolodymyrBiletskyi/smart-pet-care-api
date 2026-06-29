using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.NotificationModule.Domain
{
    public interface INotificationService
    {
        Task SendReminderNotificationAsync(Reminder reminder, CancellationToken ct);
    }
}
