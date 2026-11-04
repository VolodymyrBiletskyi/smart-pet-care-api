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

        /// <summary>
        /// How long an occurrence stays open before it counts as missed. A day, because no rule
        /// the calculator can express fires twice in one — the bound the completion duplicate
        /// guard already relies on. So a run older than this cannot still be the current
        /// occurrence of its rule, whatever the rule is, and for a Daily one the deadline falls
        /// exactly where the next firing used to move it.
        /// </summary>
        private static readonly TimeSpan MissedGrace = TimeSpan.FromHours(24);

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

        /// <summary>
        /// One tick. Internal so a test can drive the whole thing — the sweep and the firing
        /// are each reachable on their own, but nothing would otherwise catch the call to one
        /// of them going missing from here.
        /// </summary>
        internal async Task ProcessDueRemindersAsync()
        {
            var now = DateTime.UtcNow;

            await SweepMissedRunsAsync(now);

            List<Guid> dueIds;
            using (var queryScope = _scopeFactory.CreateScope())
            {
                var queryRepo = queryScope.ServiceProvider.GetRequiredService<IReminderRepository>();
                dueIds = (await queryRepo.GetDueRemindersAsync(now)).Select(r => r.Id).ToList();
            }

            foreach (var reminderId in dueIds)
            {
                // One scope, and so one DbContext, per reminder. EF leaves a failed insert in the
                // change tracker, so a shared context would carry a rejected run into the next
                // reminder's SaveChanges and take down every reminder queued behind it.
                using var scope = _scopeFactory.CreateScope();
                var reminderRepo = scope.ServiceProvider.GetRequiredService<IReminderRepository>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                try
                {
                    var reminder = await reminderRepo.GetByIdAsync(reminderId);
                    if (!IsStillDue(reminder, now)) continue;

                    await FireReminderAsync(reminder!, now, reminderRepo, notificationService);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process reminder {ReminderId}", reminderId);
                }
            }
        }

        /// <summary>
        /// An occurrence nobody confirmed is closed on a clock, not only when the next one
        /// fires. <see cref="FireReminderAsync"/>'s own sweep cannot reach the last run of a
        /// series — a Once rule, and the final occurrence before <c>EndAt</c>, have no next
        /// firing, so their run sat in Sent for good and the client had no way to tell it from
        /// one still awaiting an answer. For a weekly or monthly rule it also meant "missed"
        /// appeared a week or a month late.
        ///
        /// Marking a run missed does not close the door on a late confirmation:
        /// <c>GetLatestOpenRunAsync</c> excludes only Completed and Cancelled, so a Done tap
        /// the next morning still lands on that same run and carries it to Completed.
        /// </summary>
        private async Task SweepMissedRunsAsync(DateTime now)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminderRepo = scope.ServiceProvider.GetRequiredService<IReminderRepository>();

                var closed = await SweepMissedRunsAsync(reminderRepo, now);
                if (closed > 0)
                    _logger.LogInformation("Closed {Count} unconfirmed reminder runs as missed", closed);
            }
            catch (Exception ex)
            {
                // Firing is the job that matters, and the sweep is bookkeeping that will be
                // retried in 30 seconds; letting it throw here would cost this tick's pushes.
                _logger.LogError(ex, "Failed to sweep unconfirmed reminder runs");
            }
        }

        internal static Task<int> SweepMissedRunsAsync(IReminderRepository reminderRepo, DateTime now) =>
            reminderRepo.MarkOverdueRunsMissedAsync(now - MissedGrace);

        /// <summary>
        /// Re-checked against this tick's <paramref name="now"/> rather than merely for presence:
        /// the loop does database writes and real FCM calls, so a completion can land between the
        /// due query and this reminder's turn and move the trigger into the future. Firing that
        /// slot would push a notification nobody is owed, re-set OverdueSince, overwrite the
        /// recalculated trigger with a nag date, and write a run whose SentAt precedes its
        /// ScheduledFor — which CK_ReminderRun_TimingOrder rejects, so the save throws, the
        /// per-reminder catch swallows it and the run stays Pending for good.
        /// </summary>
        internal static bool IsStillDue(Reminder? reminder, DateTime now) =>
            reminder is not null
            && reminder.Status == ReminderStatus.Active
            && reminder.NextTriggerAt is { } trigger
            && trigger <= now;

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
