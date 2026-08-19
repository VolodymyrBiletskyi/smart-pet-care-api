using Xunit;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Tests;

public class ReminderRecalculationServiceTests
{
    private const int MoscowOffset = 180;

    private static Reminder BuildReminder(
        RecalcStrategy strategy,
        RepeatType repeatType = RepeatType.Daily,
        int intervalN = 30,
        ReminderType type = ReminderType.ParasiteTreatment,
        DaysOfWeek[]? days = null,
        DateTime? nextTriggerAt = null,
        DateTime? endAt = null)
    {
        var anchor = new DateTime(2026, 3, 1, 5, 0, 0, DateTimeKind.Utc);

        return new Reminder
        {
            PetId = Guid.NewGuid(),
            Title = "Antiparasitic",
            Type = type,
            RepeatType = repeatType,
            IntervalN = intervalN,
            RecalcStrategy = strategy,
            Days = days ?? [],
            TimeOfDay = new TimeSpan(5, 0, 0),
            UtcOffsetMinutes = MoscowOffset,
            StartAt = anchor,
            ScheduleAnchorAt = anchor,
            NextTriggerAt = nextTriggerAt ?? anchor,
            EndAt = endAt,
            Status = ReminderStatus.Active
        };
    }

    private static (ReminderRecalculationService Service, FakeReminderRepository Repo) BuildService(Reminder reminder)
    {
        var repo = new FakeReminderRepository();
        repo.Reminders.Add(reminder);
        return (new ReminderRecalculationService(repo), repo);
    }

    [Fact]
    public async Task Completion_moves_the_trigger_by_the_interval_from_the_actual_date()
    {
        // The user is three days late. The old behaviour kept the calendar date and left the
        // pet unprotected for those three days; the new one counts from what happened.
        var reminder = BuildReminder(RecalcStrategy.FromCompletion);
        var (service, _) = BuildService(reminder);

        var performedAt = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc);
        var outcome = await service.RegisterCompletionAsync(reminder.Id, performedAt);

        Assert.NotNull(outcome);
        // 4 March + 30 days, at the rule's 08:00 local
        Assert.Equal(new DateTime(2026, 4, 3, 5, 0, 0, DateTimeKind.Utc), reminder.NextTriggerAt);
    }

    [Fact]
    public async Task Completion_records_the_performed_date_and_clears_the_overdue_flag()
    {
        var reminder = BuildReminder(RecalcStrategy.FromCompletion);
        reminder.OverdueSince = new DateTime(2026, 3, 1, 5, 0, 0, DateTimeKind.Utc);
        var (service, _) = BuildService(reminder);

        var performedAt = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc);
        await service.RegisterCompletionAsync(reminder.Id, performedAt);

        Assert.Null(reminder.OverdueSince);
        Assert.Equal(performedAt, reminder.LastCompletedAt);
    }

    [Fact]
    public async Task Completion_moves_the_anchor_so_the_interval_counts_from_the_new_date()
    {
        var reminder = BuildReminder(RecalcStrategy.FromCompletion);
        var (service, _) = BuildService(reminder);

        await service.RegisterCompletionAsync(
            reminder.Id, new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(reminder.NextTriggerAt, reminder.ScheduleAnchorAt);
    }

    [Fact]
    public async Task Calendar_rules_keep_a_pending_future_trigger()
    {
        // Confirming a brushing records the fact; it does not move the routine.
        var pending = DateTime.UtcNow.AddDays(2);
        var reminder = BuildReminder(
            RecalcStrategy.Calendar, type: ReminderType.Brushing, nextTriggerAt: pending);
        var (service, _) = BuildService(reminder);

        await service.RegisterCompletionAsync(reminder.Id, DateTime.UtcNow.AddHours(-1));

        Assert.Equal(pending, reminder.NextTriggerAt);
    }

    [Fact]
    public async Task Completing_before_the_notification_materialises_the_pending_slot()
    {
        // Bathed the dog on Thursday for a reminder due Saturday: there is no run to close yet.
        var pending = DateTime.UtcNow.AddDays(2);
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, nextTriggerAt: pending);
        var (service, repo) = BuildService(reminder);

        var outcome = await service.RegisterCompletionAsync(reminder.Id, DateTime.UtcNow);

        Assert.NotNull(outcome);
        var run = Assert.Single(repo.Runs);
        Assert.Equal(pending, run.ScheduledFor);
        Assert.Equal(ReminderRunStatus.Completed, run.Status);
    }

    [Fact]
    public async Task Completing_closes_the_occurrence_that_already_fired()
    {
        var reminder = BuildReminder(RecalcStrategy.FromCompletion);
        var (service, repo) = BuildService(reminder);

        var fired = new ReminderRun
        {
            ReminderId = reminder.Id,
            ScheduledFor = DateTime.UtcNow.AddHours(-3),
            SentAt = DateTime.UtcNow.AddHours(-3),
            Status = ReminderRunStatus.Sent
        };
        repo.Runs.Add(fired);

        await service.RegisterCompletionAsync(reminder.Id, DateTime.UtcNow.AddHours(-1));

        Assert.Single(repo.Runs);
        Assert.Equal(ReminderRunStatus.Completed, fired.Status);
    }

    [Fact]
    public async Task Performed_date_is_kept_apart_from_the_confirmation_time()
    {
        // The user confirms today that the treatment happened last week. Storing the corrected
        // date in CompletedAt would put it before SentAt and break the run's timing check.
        var reminder = BuildReminder(RecalcStrategy.FromCompletion);
        var (service, repo) = BuildService(reminder);

        repo.Runs.Add(new ReminderRun
        {
            ReminderId = reminder.Id,
            ScheduledFor = DateTime.UtcNow.AddDays(-1),
            SentAt = DateTime.UtcNow.AddDays(-1),
            Status = ReminderRunStatus.Sent
        });

        var performedAt = DateTime.UtcNow.AddDays(-7);
        var outcome = await service.RegisterCompletionAsync(reminder.Id, performedAt);

        Assert.Equal(performedAt, outcome!.Run.PerformedAt);
        Assert.True(outcome.Run.CompletedAt >= outcome.Run.SentAt);
    }

    [Fact]
    public async Task Registering_the_same_completion_twice_changes_nothing()
    {
        // Done is tapped and the client also posts a health record with the same reminderId.
        var reminder = BuildReminder(RecalcStrategy.FromCompletion);
        var (service, repo) = BuildService(reminder);

        var performedAt = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc);
        await service.RegisterCompletionAsync(reminder.Id, performedAt);
        var triggerAfterFirst = reminder.NextTriggerAt;

        var second = await service.RegisterCompletionAsync(reminder.Id, performedAt);

        Assert.True(second!.AlreadyRecorded);
        Assert.Single(repo.Runs);
        Assert.Equal(triggerAfterFirst, reminder.NextTriggerAt);
    }

    [Fact]
    public async Task A_second_completion_the_same_day_does_not_skip_an_occurrence()
    {
        // Done is tapped on the push, then the user changes their mind and saves the feeding log
        // too. The log carries its own fedAt, seconds later. Treating that as a fresh completion
        // would materialise the *next* occurrence and close it, moving the schedule twice.
        var reminder = BuildReminder(RecalcStrategy.FromCompletion, type: ReminderType.Feeding);
        var (service, repo) = BuildService(reminder);

        await service.RegisterCompletionAsync(
            reminder.Id, new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc));
        var triggerAfterFirst = reminder.NextTriggerAt;

        var second = await service.RegisterCompletionAsync(
            reminder.Id, new DateTime(2026, 3, 4, 12, 0, 40, DateTimeKind.Utc));

        Assert.True(second!.AlreadyRecorded);
        Assert.Single(repo.Runs);
        Assert.Equal(triggerAfterFirst, reminder.NextTriggerAt);
    }

    [Fact]
    public async Task Completions_are_told_apart_by_the_users_day_not_by_a_fixed_gap()
    {
        // 23:00 and 01:00 Moscow are two hours apart and belong to two different days. A window
        // measured in hours would merge them; the rule is one occurrence per local day.
        var reminder = BuildReminder(RecalcStrategy.Calendar, type: ReminderType.Feeding);
        var (service, repo) = BuildService(reminder);

        await service.RegisterCompletionAsync(
            reminder.Id, new DateTime(2026, 3, 4, 20, 0, 0, DateTimeKind.Utc));

        var second = await service.RegisterCompletionAsync(
            reminder.Id, new DateTime(2026, 3, 4, 22, 0, 0, DateTimeKind.Utc));

        Assert.False(second!.AlreadyRecorded);
        Assert.Equal(2, repo.Runs.Count);
    }

    [Fact]
    public async Task Completion_past_the_end_date_finishes_the_series()
    {
        var reminder = BuildReminder(
            RecalcStrategy.FromCompletion,
            endAt: new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc));
        var (service, _) = BuildService(reminder);

        await service.RegisterCompletionAsync(
            reminder.Id, new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ReminderStatus.Completed, reminder.Status);
        Assert.Null(reminder.NextTriggerAt);
    }

    [Fact]
    public async Task Completion_snapshots_the_type_onto_the_run()
    {
        // History must survive the rule being re-typed later.
        var reminder = BuildReminder(RecalcStrategy.Calendar, type: ReminderType.NailTrimming);
        var (service, _) = BuildService(reminder);

        var outcome = await service.RegisterCompletionAsync(reminder.Id, DateTime.UtcNow);

        Assert.Equal(ReminderType.NailTrimming, outcome!.Run.Type);
    }

    [Fact]
    public async Task Unknown_reminder_returns_null()
    {
        var (service, _) = BuildService(BuildReminder(RecalcStrategy.Calendar));

        Assert.Null(await service.RegisterCompletionAsync(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public async Task Reminder_belonging_to_another_pet_is_rejected()
    {
        var reminder = BuildReminder(RecalcStrategy.Calendar);
        var (service, _) = BuildService(reminder);

        var outcome = await service.RegisterCompletionAsync(
            reminder.Id, DateTime.UtcNow, expectedPetId: Guid.NewGuid());

        Assert.Null(outcome);
    }
}
