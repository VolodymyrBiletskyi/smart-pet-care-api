using smart_pet_care_api.Models;
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

            var now = DateTime.UtcNow;
            var dueReminders = await reminderRepo.GetDueRemindersAsync(now);

            foreach (var reminder in dueReminders)
            {
                try
                {
                    await FireReminderAsync(reminder, now, reminderRepo);
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
            IReminderRepository reminderRepo)
        {
            await reminderRepo.AddRunAsync(new ReminderRun
            {
                ReminderId = reminder.Id,
                ScheduledFor = reminder.NextTriggerAt!.Value,
                Status = ReminderRunStatus.Sent,
                SentAt = now,
                Channel = "push"
            });

            if (!reminder.IsRepeatable)
            {
                reminder.Status = ReminderStatus.Completed;
                reminder.NextTriggerAt = null;
            }
            else
            {
                var next = ReminderService.ComputeNextTrigger(reminder.Days, reminder.TimeOfDay, now);

                if (next == null || (reminder.EndAt.HasValue && next > reminder.EndAt))
                {
                    reminder.Status = ReminderStatus.Completed;
                    reminder.NextTriggerAt = null;
                }
                else
                {
                    reminder.NextTriggerAt = next;
                }
            }

            reminder.UpdatedAt = now;
            await reminderRepo.SaveChangesAsync();
        }
    }
}
