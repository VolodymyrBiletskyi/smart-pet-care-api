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

        internal static async Task FireReminderAsync(
            Reminder reminder,
            DateTime now,
            IReminderRepository reminderRepo,
            INotificationService notificationService)
        {
            var scheduledFor = reminder.NextTriggerAt!.Value;

            // A new occurrence is due, so any earlier one still waiting for confirmation will
            // never get it. Without this they sit in Sent forever and history cannot tell a
            // delivered-and-done occurrence from a delivered-and-ignored one.
            foreach (var stale in await reminderRepo.GetUnconfirmedRunsAsync(reminder.Id, scheduledFor))
            {
                stale.Status = ReminderRunStatus.Missed;
                stale.UpdatedAt = now;
            }

            var run = new ReminderRun
            {
                ReminderId = reminder.Id,
                ScheduledFor = scheduledFor,
                Status = ReminderRunStatus.Pending,
                Channel = "push",
                // Snapshotted so editing the rule later cannot recategorise finished history.
                Type = reminder.Type
            };
            await reminderRepo.AddRunAsync(run);

            // Firing is not completing. Completion-driven rules stay marked until the user
            // confirms — that is the whole point, a missed antiparasitic must keep asking
            // instead of quietly rescheduling a month out. Calendar rules are left alone:
            // nobody confirms every brushing, and flagging those would leave the pet
            // permanently overdue.
            if (reminder.RecalcStrategy != RecalcStrategy.Calendar)
                reminder.OverdueSince ??= scheduledFor;

            // Computed from now rather than from the missed slot, so a scheduler that was down
            // for three days resumes instead of replaying three days of stale notifications.
            var next = ReminderScheduleCalculator.NextAfterMiss(
                ReminderScheduleCalculator.PlanFor(reminder), now);

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

            var sent = await notificationService.SendReminderNotificationAsync(
                reminder, run.ScheduledFor, CancellationToken.None);

            run.Status = sent ? ReminderRunStatus.Sent : ReminderRunStatus.Failed;
            run.SentAt = sent ? now : null;
            run.UpdatedAt = now;
            await reminderRepo.SaveChangesAsync();
        }
    }
}
