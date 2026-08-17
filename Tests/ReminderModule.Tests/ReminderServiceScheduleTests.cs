using Xunit;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Tests;

public class ReminderServiceScheduleTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static (ReminderService Service, FakeReminderRepository Repo) Build()
    {
        var repo = new FakeReminderRepository();
        return (new ReminderService(repo, new FakePetRepository()), repo);
    }

    private static CreateReminderDto Dto(
        ReminderType type = ReminderType.Bathing,
        RepeatType repeatType = RepeatType.Weekly,
        int intervalN = 1,
        RecalcStrategy strategy = RecalcStrategy.Calendar,
        DaysOfWeek[]? days = null,
        DateOnly? date = null) => new()
        {
            PetId = Guid.NewGuid(),
            Title = "Bath",
            Type = type,
            RepeatType = repeatType,
            IntervalN = intervalN,
            RecalcStrategy = strategy,
            Days = days ?? (repeatType == RepeatType.Weekly ? [DaysOfWeek.Saturday] : []),
            Date = date,
            Time = new TimeOnly(8, 0),
            UtcOffsetMinutes = 180
        };

    [Fact]
    public async Task Medical_types_are_pinned_to_completion_driven_recalculation()
    {
        // A client asking for Calendar on a vaccination does not get it: the interval is a
        // safety property and is not the client's to choose.
        var (service, _) = Build();

        var created = await service.CreateAsync(
            Dto(ReminderType.Vaccination, RepeatType.Monthly, 12, RecalcStrategy.Calendar,
                days: [], date: new DateOnly(2026, 3, 10)),
            UserId);

        Assert.Equal(RecalcStrategy.FromCompletion, created.RecalcStrategy);
    }

    [Fact]
    public async Task Grooming_types_keep_the_strategy_the_client_asked_for()
    {
        var (service, _) = Build();

        var created = await service.CreateAsync(
            Dto(ReminderType.Bathing, RepeatType.Weekly, 2,
                RecalcStrategy.FromCompletionAlignedToWeekday, [DaysOfWeek.Saturday]),
            UserId);

        Assert.Equal(RecalcStrategy.FromCompletionAlignedToWeekday, created.RecalcStrategy);
        Assert.Equal(2, created.IntervalN);
    }

    [Fact]
    public async Task The_anchor_starts_at_the_first_occurrence()
    {
        var (service, _) = Build();

        var created = await service.CreateAsync(Dto(), UserId);

        Assert.Equal(created.StartAt, created.ScheduleAnchorAt);
        Assert.Equal(created.NextTriggerAt, created.ScheduleAnchorAt);
    }

    [Fact]
    public async Task Existing_clients_that_send_no_interval_keep_the_old_behaviour()
    {
        var (service, _) = Build();

        var created = await service.CreateAsync(Dto(), UserId);

        Assert.Equal(1, created.IntervalN);
        Assert.Equal(RecalcStrategy.Calendar, created.RecalcStrategy);
    }

    [Fact]
    public async Task Weekday_alignment_allows_days_on_a_daily_rule()
    {
        // "Every 30 days, and let it land on a Saturday" is the grooming case; days are the
        // alignment target here, not extra triggers.
        var (service, _) = Build();

        var created = await service.CreateAsync(
            Dto(ReminderType.Bathing, RepeatType.Daily, 30,
                RecalcStrategy.FromCompletionAlignedToWeekday, [DaysOfWeek.Saturday]),
            UserId);

        Assert.Equal([DaysOfWeek.Saturday], created.Days);
    }

    [Fact]
    public async Task A_daily_rule_without_alignment_still_rejects_days()
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            Dto(ReminderType.Brushing, RepeatType.Daily, 1, RecalcStrategy.Calendar,
                [DaysOfWeek.Saturday]),
            UserId));
    }

    [Fact]
    public async Task Weekday_alignment_requires_at_least_one_day()
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            Dto(ReminderType.Bathing, RepeatType.Daily, 30,
                RecalcStrategy.FromCompletionAlignedToWeekday, []),
            UserId));
    }

    [Fact]
    public async Task Once_reminders_cannot_recalculate_from_completion()
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            Dto(ReminderType.Grooming, RepeatType.Once, 1, RecalcStrategy.FromCompletion,
                days: [], date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))),
            UserId));
    }

    [Fact]
    public async Task A_one_off_vet_visit_is_left_on_the_calendar_despite_being_medical()
    {
        // The medical pin only applies to repeating rules — a single visit has no interval.
        var (service, _) = Build();

        var created = await service.CreateAsync(
            Dto(ReminderType.VetVisit, RepeatType.Once, 1, RecalcStrategy.Calendar,
                days: [], date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))),
            UserId);

        Assert.Equal(RecalcStrategy.Calendar, created.RecalcStrategy);
    }

    [Theory]
    [InlineData(RepeatType.Daily, 366)]
    [InlineData(RepeatType.Weekly, 53)]
    [InlineData(RepeatType.Monthly, 25)]
    [InlineData(RepeatType.Daily, 0)]
    public async Task Out_of_range_intervals_are_rejected(RepeatType repeatType, int intervalN)
    {
        var (service, _) = Build();

        var dto = Dto(ReminderType.Brushing, repeatType, intervalN,
            date: repeatType == RepeatType.Monthly ? new DateOnly(2026, 3, 10) : null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(dto, UserId));
    }

    [Fact]
    public async Task Editing_the_schedule_restarts_the_anchor_and_clears_the_overdue_flag()
    {
        var (service, repo) = Build();
        var created = await service.CreateAsync(Dto(), UserId);
        repo.Reminders[0].OverdueSince = DateTime.UtcNow.AddDays(-2);

        var updated = await service.UpdateAsync(
            created.Id, new PatchReminderDto { IntervalN = 3 }, UserId);

        Assert.Equal(3, updated.IntervalN);
        Assert.Equal(updated.NextTriggerAt, updated.ScheduleAnchorAt);
        Assert.Null(updated.OverdueSince);
    }
}
