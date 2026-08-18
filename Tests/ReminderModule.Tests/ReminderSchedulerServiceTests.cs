using Xunit;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NotificationModule.Domain;
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
}
