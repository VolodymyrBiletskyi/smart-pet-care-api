using Xunit;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using static smart_pet_care_api.Models.Enums;
using static smart_pet_care_api.Modules.ReminderModule.Domain.ReminderScheduleCalculator;

namespace smart_pet_care_api.Modules.ReminderModule.Tests;

public class ReminderScheduleCalculatorTests
{
    private const int MoscowOffset = 180;
    private const int HawaiiOffset = -600;

    /// <summary>Builds a plan the way the service does: TimeOfDay is stored in UTC.</summary>
    private static SchedulePlan Plan(
        RepeatType repeatType,
        int intervalN = 1,
        RecalcStrategy strategy = RecalcStrategy.Calendar,
        DaysOfWeek[]? days = null,
        DateOnly? date = null,
        int localHour = 8,
        int offsetMinutes = MoscowOffset,
        DateTime anchorUtc = default)
    {
        var timeOfDayUtc = new TimeOnly(localHour, 0)
            .Add(TimeSpan.FromMinutes(-offsetMinutes))
            .ToTimeSpan();

        return new SchedulePlan(
            repeatType, intervalN, days ?? [], date, timeOfDayUtc, offsetMinutes, anchorUtc, strategy);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    // ---- from-completion -------------------------------------------------------------

    [Fact]
    public void FromCompletion_daily_adds_the_interval_in_days()
    {
        // Antiparasitic cover is a fixed number of days from the dose, so 30 means 30 days
        // and not "next month".
        var plan = Plan(RepeatType.Daily, intervalN: 30, strategy: RecalcStrategy.FromCompletion);

        // 10 March 22:00 local
        var next = NextFromCompletion(plan, Utc(2026, 3, 10, 19));

        // 9 April, 08:00 local
        Assert.Equal(Utc(2026, 4, 9, 5), next);
    }

    [Fact]
    public void FromCompletion_keeps_the_rules_time_of_day_not_the_moment_of_confirmation()
    {
        var plan = Plan(RepeatType.Daily, intervalN: 30, strategy: RecalcStrategy.FromCompletion);

        var next = NextFromCompletion(plan, Utc(2026, 3, 10, 20, 40));

        Assert.Equal(new TimeSpan(5, 0, 0), next!.Value.TimeOfDay);
    }

    [Fact]
    public void FromCompletion_monthly_handles_a_yearly_interval_as_twelve_months()
    {
        var plan = Plan(RepeatType.Monthly, intervalN: 12, strategy: RecalcStrategy.FromCompletion,
            date: new DateOnly(2026, 3, 10));

        var next = NextFromCompletion(plan, Utc(2026, 3, 10, 5));

        Assert.Equal(Utc(2027, 3, 10, 5), next);
    }

    [Fact]
    public void FromCompletion_uses_the_local_date_even_when_it_differs_from_the_utc_date()
    {
        // 10 March 19:00 in UTC-10 is already 11 March in UTC. Counting from the UTC date
        // would land the next occurrence a day late.
        var plan = Plan(RepeatType.Daily, intervalN: 7, strategy: RecalcStrategy.FromCompletion,
            localHour: 22, offsetMinutes: HawaiiOffset);

        var next = NextFromCompletion(plan, Utc(2026, 3, 11, 5));

        // 17 March 22:00 local, which is 18 March 08:00 UTC
        Assert.Equal(Utc(2026, 3, 18, 8), next);
    }

    // ---- weekday alignment -----------------------------------------------------------

    [Fact]
    public void Aligned_strategy_moves_forward_to_the_selected_weekday()
    {
        // Bathing every two weeks, habitually on Saturdays. 10 March 2026 is a Tuesday, so
        // the raw result lands on Tuesday 24 March and is nudged to Saturday 28 March.
        var plan = Plan(RepeatType.Daily, intervalN: 14,
            strategy: RecalcStrategy.FromCompletionAlignedToWeekday,
            days: [DaysOfWeek.Saturday]);

        var next = NextFromCompletion(plan, Utc(2026, 3, 10, 5));

        Assert.Equal(Utc(2026, 3, 28, 5), next);
    }

    [Fact]
    public void Aligned_strategy_never_moves_backwards()
    {
        // The nearest Saturday to Tuesday 24 March is the 21st, three days earlier. Taking it
        // would shorten the interval to 11 days, which is the harm the interval exists to
        // prevent, so the later Saturday wins.
        var plan = Plan(RepeatType.Daily, intervalN: 14,
            strategy: RecalcStrategy.FromCompletionAlignedToWeekday,
            days: [DaysOfWeek.Saturday]);

        var next = NextFromCompletion(plan, Utc(2026, 3, 10, 5));

        Assert.True(next > Utc(2026, 3, 24, 5));
    }

    [Fact]
    public void Aligned_strategy_leaves_a_date_that_already_falls_on_a_selected_day()
    {
        // 7 March 2026 is a Saturday, so +14 days is a Saturday too and nothing should shift.
        var plan = Plan(RepeatType.Daily, intervalN: 14,
            strategy: RecalcStrategy.FromCompletionAlignedToWeekday,
            days: [DaysOfWeek.Saturday]);

        var next = NextFromCompletion(plan, Utc(2026, 3, 7, 5));

        Assert.Equal(Utc(2026, 3, 21, 5), next);
    }

    // ---- calendar --------------------------------------------------------------------

    [Fact]
    public void Weekly_with_interval_one_takes_the_next_selected_day()
    {
        var plan = Plan(RepeatType.Weekly, days: [DaysOfWeek.Saturday],
            anchorUtc: Utc(2026, 3, 7, 5));

        var next = NextFromCalendar(plan, Utc(2026, 3, 8, 5));

        Assert.Equal(Utc(2026, 3, 14, 5), next);
    }

    [Fact]
    public void Weekly_with_interval_two_skips_the_odd_week_relative_to_the_anchor()
    {
        // The anchor is Saturday 7 March, so 14 March is an off week and 21 March is the next
        // one that fires.
        var plan = Plan(RepeatType.Weekly, intervalN: 2, days: [DaysOfWeek.Saturday],
            anchorUtc: Utc(2026, 3, 7, 5));

        var next = NextFromCalendar(plan, Utc(2026, 3, 8, 5));

        Assert.Equal(Utc(2026, 3, 21, 5), next);
    }

    [Fact]
    public void Weekly_returns_a_later_day_in_the_same_week_before_moving_on()
    {
        var plan = Plan(RepeatType.Weekly,
            days: [DaysOfWeek.Monday, DaysOfWeek.Wednesday, DaysOfWeek.Friday],
            anchorUtc: Utc(2026, 3, 2, 5));

        // Monday 9 March, just after the 08:00 local trigger
        var next = NextFromCalendar(plan, Utc(2026, 3, 9, 6));

        // Wednesday 11 March
        Assert.Equal(Utc(2026, 3, 11, 5), next);
    }

    [Fact]
    public void Daily_with_interval_counts_from_the_anchor()
    {
        var plan = Plan(RepeatType.Daily, intervalN: 3, anchorUtc: Utc(2026, 3, 1, 5));

        var next = NextFromCalendar(plan, Utc(2026, 3, 5, 5));

        // 1, 4, 7 March — 5 March falls between, so the 7th is next.
        Assert.Equal(Utc(2026, 3, 7, 5), next);
    }

    [Fact]
    public void Monthly_clamps_a_29_february_anchor_in_a_non_leap_year()
    {
        var plan = Plan(RepeatType.Monthly, intervalN: 12, date: new DateOnly(2024, 2, 29),
            anchorUtc: Utc(2024, 2, 29, 5));

        var next = NextFromCalendar(plan, Utc(2024, 3, 1, 5));

        Assert.Equal(Utc(2025, 2, 28, 5), next);
    }

    [Fact]
    public void Monthly_with_interval_one_is_unchanged()
    {
        var plan = Plan(RepeatType.Monthly, date: new DateOnly(2026, 3, 15),
            anchorUtc: Utc(2026, 3, 15, 5));

        var next = NextFromCalendar(plan, Utc(2026, 3, 16, 5));

        Assert.Equal(Utc(2026, 4, 15, 5), next);
    }

    [Fact]
    public void Once_never_produces_a_next_occurrence()
    {
        var plan = Plan(RepeatType.Once, date: new DateOnly(2026, 3, 15));

        Assert.Null(NextFromCalendar(plan, Utc(2026, 3, 1)));
        Assert.Null(NextFromCompletion(plan, Utc(2026, 3, 1)));
    }

    [Fact]
    public void FirstTrigger_ignores_the_interval_so_a_new_rule_starts_at_the_first_slot()
    {
        var plan = Plan(RepeatType.Weekly, intervalN: 4, days: [DaysOfWeek.Saturday]);

        // Sunday 8 March: the first Saturday is the 14th, not four weeks out.
        var first = FirstTrigger(plan, Utc(2026, 3, 8, 5));

        Assert.Equal(Utc(2026, 3, 14, 5), first);
    }

    // ---- misses ----------------------------------------------------------------------

    [Fact]
    public void Missed_completion_driven_occurrence_comes_back_the_next_day()
    {
        var plan = Plan(RepeatType.Daily, intervalN: 30, strategy: RecalcStrategy.FromCompletion);

        var next = NextAfterMiss(plan, Utc(2026, 3, 10, 5));

        Assert.Equal(Utc(2026, 3, 11, 5), next);
    }

    [Fact]
    public void Missed_aligned_occurrence_comes_back_on_the_next_selected_weekday()
    {
        // Not a whole interval later: a skipped bath should reappear within the week.
        var plan = Plan(RepeatType.Daily, intervalN: 30,
            strategy: RecalcStrategy.FromCompletionAlignedToWeekday,
            days: [DaysOfWeek.Saturday]);

        // Tuesday 10 March
        var next = NextAfterMiss(plan, Utc(2026, 3, 10, 5));

        // Saturday 14 March
        Assert.Equal(Utc(2026, 3, 14, 5), next);
    }

    [Fact]
    public void Missed_calendar_occurrence_just_rolls_on()
    {
        var plan = Plan(RepeatType.Daily, anchorUtc: Utc(2026, 3, 1, 5));

        var next = NextAfterMiss(plan, Utc(2026, 3, 10, 5));

        Assert.Equal(Utc(2026, 3, 11, 5), next);
    }
}
