using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NotificationModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Scheduler
{
    public class ReminderSchedulerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderSchedulerService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        public ReminderSchedulerService(IServiceScopeFactory scopeFactory, ILogger<ReminderSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueRemindersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in reminder scheduler tick");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task ProcessDueRemindersAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var reminderRepo = scope.ServiceProvider.GetRequiredService<IReminderRepository>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTime.UtcNow;
            var dueReminders = await reminderRepo.GetDueRemindersAsync(now);

            foreach (var reminder in dueReminders)
            {
                try
                {
                    await FireReminderAsync(reminder, now, reminderRepo, notificationService);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process reminder {ReminderId}", reminder.Id);
                }
            }
        }

        private static async Task FireReminderAsync(
            Reminder reminder,
            DateTime now,
            IReminderRepository reminderRepo,
            INotificationService notificationService)
        {
            await reminderRepo.AddRunAsync(new ReminderRun
            {
                ReminderId = reminder.Id,
                ScheduledFor = reminder.NextTriggerAt!.Value,
                Status = ReminderRunStatus.Sent,
                SentAt = now,
                Channel = "push"
            });

            await notificationService.SendReminderNotificationAsync(reminder, CancellationToken.None);

            var next = ComputeNextTrigger(reminder, now);

            if (next == null || (reminder.EndAt.HasValue && next > reminder.EndAt))
            {
                reminder.Status = ReminderStatus.Completed;
                reminder.NextTriggerAt = null;
            }
            else
            {
                reminder.NextTriggerAt = next;
            }

            reminder.UpdatedAt = now;
            await reminderRepo.SaveChangesAsync();
        }

        private static DateTime? ComputeNextTrigger(Reminder reminder, DateTime now) => reminder.RepeatType switch
        {
            RepeatType.Once => null,
            RepeatType.Daily => ReminderService.ComputeNextDaily(reminder.TimeOfDay, now),
            RepeatType.Weekly => ReminderService.ComputeNextTrigger(reminder.Days, reminder.TimeOfDay, now),
            RepeatType.Monthly => ReminderService.ComputeNextMonthly(
                reminder.Date!.Value,
                TimeOnly.FromDateTime(reminder.NextTriggerAt!.Value.AddMinutes(reminder.UtcOffsetMinutes)),
                reminder.UtcOffsetMinutes,
                now),
            _ => null,
        };
    }
}
