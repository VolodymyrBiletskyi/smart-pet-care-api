using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NotificationModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.Scheduler;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Tests;

public class ReminderSchedulerServiceTests
{
    private sealed class FakeNotificationService : INotificationService
    {
        public bool Delivers { get; set; } = true;

        public Task<bool> SendReminderNotificationAsync(
            Reminder reminder, DateTime scheduledFor, CancellationToken ct) => Task.FromResult(Delivers);
    }

    private static Reminder BuildReminder(RecalcStrategy strategy, DateTime nextTriggerAt) => new()
    {
        PetId = Guid.NewGuid(),
        Title = "Antiparasitic",
        Type = ReminderType.ParasiteTreatment,
        RepeatType = RepeatType.Daily,
        IntervalN = 30,
        RecalcStrategy = strategy,
        TimeOfDay = new TimeSpan(5, 0, 0),
        UtcOffsetMinutes = 180,
        StartAt = nextTriggerAt,
        ScheduleAnchorAt = nextTriggerAt,
        NextTriggerAt = nextTriggerAt,
        Status = ReminderStatus.Active
    };

    [Fact]
    public async Task Firing_creates_a_run_carrying_the_rules_type()
    {
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, DateTime.UtcNow.AddMinutes(-1));
        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        await ReminderSchedulerService.FireReminderAsync(
            reminder, DateTime.UtcNow, repo, new FakeNotificationService());

        var run = Assert.Single(repo.Runs);
        Assert.Equal(ReminderType.ParasiteTreatment, run.Type);
        Assert.Equal(ReminderRunStatus.Sent, run.Status);
    }

    [Fact]
    public async Task Firing_marks_the_previous_unconfirmed_occurrence_missed()
    {
        // Without this the earlier occurrence sits in Sent forever and history cannot tell a
        // delivered-and-done occurrence from a delivered-and-ignored one.
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, DateTime.UtcNow.AddMinutes(-1));
        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        var earlier = new ReminderRun
        {
            ReminderId = reminder.Id,
            ScheduledFor = DateTime.UtcNow.AddDays(-1),
            SentAt = DateTime.UtcNow.AddDays(-1),
            Status = ReminderRunStatus.Sent
        };
        repo.Runs.Add(earlier);

        await ReminderSchedulerService.FireReminderAsync(
            reminder, DateTime.UtcNow, repo, new FakeNotificationService());

        Assert.Equal(ReminderRunStatus.Missed, earlier.Status);
    }

    [Fact]
    public async Task Firing_leaves_a_confirmed_occurrence_alone()
    {
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, DateTime.UtcNow.AddMinutes(-1));
        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        var done = new ReminderRun
        {
            ReminderId = reminder.Id,
            ScheduledFor = DateTime.UtcNow.AddDays(-1),
            SentAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddDays(-1).AddHours(1),
            PerformedAt = DateTime.UtcNow.AddDays(-1),
            Status = ReminderRunStatus.Completed
        };
        repo.Runs.Add(done);

        await ReminderSchedulerService.FireReminderAsync(
            reminder, DateTime.UtcNow, repo, new FakeNotificationService());

        Assert.Equal(ReminderRunStatus.Completed, done.Status);
    }

    [Fact]
    public async Task An_early_completion_does_not_block_the_slot_the_scheduler_will_insert()
    {
        // Ticked off the afternoon before, then the scheduler reaches 09:00. Both rows land in
        // ReminderRun, so the unique (ReminderId, ScheduledFor) index means they have to differ
        // — matching ones abort the tick and wedge the reminder into a retry loop.
        var trigger = new DateTime(2026, 8, 20, 7, 0, 0, DateTimeKind.Utc);
        var reminder = new Reminder
        {
            PetId = Guid.NewGuid(),
            Title = "test block",
            Type = ReminderType.Feeding,
            RepeatType = RepeatType.Daily,
            IntervalN = 1,
            RecalcStrategy = RecalcStrategy.FromCompletion,
            TimeOfDay = new TimeSpan(7, 0, 0),
            UtcOffsetMinutes = 120,
            StartAt = trigger,
            ScheduleAnchorAt = trigger,
            NextTriggerAt = trigger,
            Status = ReminderStatus.Active
        };

        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        await new ReminderRecalculationService(repo).RegisterCompletionAsync(
            reminder.Id, new DateTime(2026, 8, 19, 13, 20, 42, DateTimeKind.Utc));

        await ReminderSchedulerService.FireReminderAsync(
            reminder, trigger, repo, new FakeNotificationService());

        Assert.Equal(2, repo.Runs.Count);
        Assert.Equal(2, repo.Runs.Select(r => r.ScheduledFor).Distinct().Count());
    }

    [Fact]
    public async Task Firing_a_completion_driven_rule_marks_it_overdue()
    {
        var scheduledFor = DateTime.UtcNow.AddMinutes(-1);
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, scheduledFor);
        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        await ReminderSchedulerService.FireReminderAsync(
            reminder, DateTime.UtcNow, repo, new FakeNotificationService());

        Assert.Equal(scheduledFor, reminder.OverdueSince);
    }

    [Fact]
    public async Task Firing_a_calendar_rule_does_not_mark_it_overdue()
    {
        // Nobody confirms every brushing; flagging those would leave the pet permanently overdue.
        var reminder = BuildReminder(RecalcStrategy.Calendar, DateTime.UtcNow.AddMinutes(-1));
        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        await ReminderSchedulerService.FireReminderAsync(
            reminder, DateTime.UtcNow, repo, new FakeNotificationService());

        Assert.Null(reminder.OverdueSince);
    }

    [Fact]
    public void A_reminder_completed_inside_the_tick_is_no_longer_due()
    {
        // The due query and this reminder's turn are separated by other reminders' DB writes and
        // FCM calls. A completion landing in that gap moves the trigger forward, and firing it
        // anyway would push a notification nobody is owed and write SentAt < ScheduledFor, which
        // the timing check constraint rejects — leaving the run Pending for good.
        var now = DateTime.UtcNow;
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, now.AddMinutes(-1));
        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        Assert.True(ReminderSchedulerService.IsStillDue(reminder, now));

        reminder.NextTriggerAt = now.AddDays(30);

        Assert.False(ReminderSchedulerService.IsStillDue(reminder, now));
    }

    [Fact]
    public void A_reminder_that_ended_inside_the_tick_is_no_longer_due()
    {
        var now = DateTime.UtcNow;
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, now.AddMinutes(-1));
        reminder.Status = ReminderStatus.Completed;

        Assert.False(ReminderSchedulerService.IsStillDue(reminder, now));
        Assert.False(ReminderSchedulerService.IsStillDue(null, now));
    }

    [Fact]
    public async Task A_failed_delivery_is_recorded_as_failed()
    {
        var reminder = BuildReminder(RecalcStrategy.Calendar, DateTime.UtcNow.AddMinutes(-1));
        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        await ReminderSchedulerService.FireReminderAsync(
            reminder, DateTime.UtcNow, repo, new FakeNotificationService { Delivers = false });

        var run = Assert.Single(repo.Runs);
        Assert.Equal(ReminderRunStatus.Failed, run.Status);
        Assert.Null(run.SentAt);
    }

    /// <summary>
    /// <paramref name="createdAt"/> defaults to the slot: the run appeared when the slot came
    /// up, which is the ordinary case. The sweep weighs both, so a test about an old slot on a
    /// fresh row says so explicitly.
    /// </summary>
    private static ReminderRun BuildRun(Guid reminderId, DateTime scheduledFor, DateTime? createdAt = null) => new()
    {
        ReminderId = reminderId,
        ScheduledFor = scheduledFor,
        SentAt = scheduledFor,
        CreatedAt = createdAt ?? scheduledFor,
        Status = ReminderRunStatus.Sent,
        Type = ReminderType.Feeding
    };

    /// <summary>
    /// The gap the time-based sweep exists for. A Once rule has no second occurrence, so the
    /// sweep inside <c>FireReminderAsync</c> is never reached and the run stayed in Sent for
    /// good — indistinguishable from one still waiting for an answer.
    /// </summary>
    [Fact]
    public async Task Sweeping_closes_a_run_no_later_firing_would_ever_reach()
    {
        var now = DateTime.UtcNow;
        var reminder = BuildReminder(RecalcStrategy.Calendar, now.AddDays(-2));
        reminder.RepeatType = RepeatType.Once;
        reminder.Status = ReminderStatus.Completed;
        reminder.NextTriggerAt = null;

        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);
        repo.Runs.Add(BuildRun(reminder.Id, now.AddDays(-2)));

        var closed = await ReminderSchedulerService.SweepMissedRunsAsync(repo, now);

        Assert.Equal(1, closed);
        Assert.Equal(ReminderRunStatus.Missed, Assert.Single(repo.Runs).Status);
    }

    [Fact]
    public async Task Sweeping_leaves_an_occurrence_still_inside_its_day_alone()
    {
        var now = DateTime.UtcNow;
        var reminder = BuildReminder(RecalcStrategy.Calendar, now.AddHours(-2));

        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);
        repo.Runs.Add(BuildRun(reminder.Id, now.AddHours(-2)));

        var closed = await ReminderSchedulerService.SweepMissedRunsAsync(repo, now);

        Assert.Equal(0, closed);
        Assert.Equal(ReminderRunStatus.Sent, Assert.Single(repo.Runs).Status);
        // A day exactly: anything shorter would overtake a Daily rule's own next firing.
        Assert.Equal(now.AddHours(-24), Assert.Single(repo.MissedSweepCutoffs));
    }

    [Fact]
    public async Task Sweeping_leaves_a_confirmed_occurrence_alone()
    {
        var now = DateTime.UtcNow;
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, now.AddDays(-3));

        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);

        var confirmed = BuildRun(reminder.Id, now.AddDays(-3));
        confirmed.Status = ReminderRunStatus.Completed;
        confirmed.CompletedAt = now.AddDays(-3).AddMinutes(10);
        repo.Runs.Add(confirmed);

        Assert.Equal(0, await ReminderSchedulerService.SweepMissedRunsAsync(repo, now));
        Assert.Equal(ReminderRunStatus.Completed, confirmed.Status);
    }

    /// <summary>
    /// The grace is the user's window to answer, so it starts when the run appears. A scheduler
    /// back from a long outage fires a trigger older than the grace — this repo has had a 27
    /// hour one — and weighing the slot alone would mark the push it just sent missed on the
    /// next tick, thirty seconds later.
    /// </summary>
    [Fact]
    public async Task Sweeping_spares_a_run_just_created_for_a_long_overdue_slot()
    {
        var now = DateTime.UtcNow;
        var missedSlot = now.AddDays(-3);
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, missedSlot);

        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);
        repo.Runs.Add(BuildRun(reminder.Id, missedSlot, createdAt: now));

        var closed = await ReminderSchedulerService.SweepMissedRunsAsync(repo, now);

        Assert.Equal(0, closed);
        Assert.Equal(ReminderRunStatus.Sent, Assert.Single(repo.Runs).Status);
    }

    /// <summary>
    /// Drives a whole tick through the real scope factory, because every test above reaches
    /// past it: deleting the sweep's call from <c>ProcessDueRemindersAsync</c> would otherwise
    /// leave the suite green.
    /// </summary>
    [Fact]
    public async Task A_tick_both_sweeps_stale_occurrences_and_fires_what_is_due()
    {
        var now = DateTime.UtcNow;

        var due = BuildReminder(RecalcStrategy.Calendar, now.AddMinutes(-1));
        var stale = BuildReminder(RecalcStrategy.Calendar, now.AddDays(-4));
        stale.Status = ReminderStatus.Completed;
        stale.NextTriggerAt = null;

        var repo = new FakeReminderRepository();
        repo.Reminders.AddRange([due, stale]);
        repo.Runs.Add(BuildRun(stale.Id, now.AddDays(-4)));

        await BuildScheduler(repo).ProcessDueRemindersAsync();

        Assert.Single(repo.MissedSweepCutoffs);
        var swept = Assert.Single(repo.Runs, r => r.ReminderId == stale.Id);
        Assert.Equal(ReminderRunStatus.Missed, swept.Status);

        var fired = Assert.Single(repo.Runs, r => r.ReminderId == due.Id);
        Assert.Equal(ReminderRunStatus.Sent, fired.Status);
    }

    private static ReminderSchedulerService BuildScheduler(FakeReminderRepository repo)
    {
        var services = new ServiceCollection()
            .AddScoped<IReminderRepository>(_ => repo)
            .AddScoped<INotificationService>(_ => new FakeNotificationService())
            .BuildServiceProvider();

        return new ReminderSchedulerService(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ReminderSchedulerService>.Instance);
    }

    /// <summary>
    /// Missed is a label on history, not a lock. The user who answers the next morning has to
    /// land on the same occurrence rather than open a second one.
    /// </summary>
    [Fact]
    public async Task A_swept_occurrence_can_still_be_confirmed_late()
    {
        var now = DateTime.UtcNow;
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, now.AddDays(-2));

        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);
        repo.Runs.Add(BuildRun(reminder.Id, now.AddDays(-2)));

        await ReminderSchedulerService.SweepMissedRunsAsync(repo, now);

        var service = new ReminderRecalculationService(repo);
        var outcome = await service.RegisterCompletionAsync(reminder.Id, now);

        Assert.NotNull(outcome);
        Assert.False(outcome!.AlreadyRecorded);
        Assert.Equal(ReminderRunStatus.Completed, Assert.Single(repo.Runs).Status);
    }
}
