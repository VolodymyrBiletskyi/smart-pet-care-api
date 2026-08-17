using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    /// <summary>
    /// The one place trigger dates are produced. The scheduler, reminder creation and the
    /// completion flow all call in here, so "every 2 weeks on Saturdays" only has to be
    /// right once.
    ///
    /// All arithmetic happens in the user's local wall clock (UTC plus
    /// <see cref="SchedulePlan.UtcOffsetMinutes"/>, Kind=Unspecified) because weekdays and
    /// calendar days only mean anything there; results are converted back to UTC at the edge.
    /// Intervals are counted from <see cref="SchedulePlan.AnchorUtc"/> rather than from
    /// "now", which is what lets week parity survive a late completion.
    /// </summary>
    public static class ReminderScheduleCalculator
    {
        /// <summary>Longest a missed occurrence waits before it nags again.</summary>
        private static readonly TimeSpan NagStep = TimeSpan.FromDays(1);

        public readonly record struct SchedulePlan(
            RepeatType RepeatType,
            int IntervalN,
            DaysOfWeek[] Days,
            DateOnly? Date,
            TimeSpan TimeOfDayUtc,
            int UtcOffsetMinutes,
            DateTime AnchorUtc,
            RecalcStrategy RecalcStrategy);

        public static SchedulePlan PlanFor(Reminder reminder) => new(
            reminder.RepeatType,
            reminder.IntervalN,
            reminder.Days,
            reminder.Date,
            reminder.TimeOfDay,
            reminder.UtcOffsetMinutes,
            reminder.ScheduleAnchorAt == default ? reminder.StartAt : reminder.ScheduleAnchorAt,
            reminder.RecalcStrategy);

        /// <summary>
        /// Next occurrence strictly after <paramref name="afterUtc"/> following the calendar,
        /// ignoring whether anything was completed. Null when the rule cannot repeat.
        /// </summary>
        public static DateTime? NextFromCalendar(SchedulePlan plan, DateTime afterUtc)
        {
            var offset = plan.UtcOffsetMinutes;
            var afterLocal = ToLocal(afterUtc, offset);
            var anchorLocal = ToLocal(plan.AnchorUtc, offset);
            var timeOfDay = LocalTimeOfDay(plan.TimeOfDayUtc, offset);
            var step = Math.Max(1, plan.IntervalN);

            switch (plan.RepeatType)
            {
                case RepeatType.Once:
                    return null;

                case RepeatType.Daily:
                    {
                        var anchor = anchorLocal.Date + timeOfDay;
                        var candidate = StepForward(anchor, afterLocal, TimeSpan.FromDays(step));
                        return ToUtc(candidate, offset);
                    }

                case RepeatType.Weekly:
                    {
                        if (plan.Days.Length == 0) return null;

                        var anchorWeek = WeekStart(anchorLocal.Date);
                        var afterWeek = WeekStart(afterLocal.Date);

                        // Weeks are grouped in blocks of N counted from the anchor's week; only
                        // the first week of each block fires.
                        var index = (int)((afterWeek - anchorWeek).TotalDays / 7);
                        var remainder = ((index % step) + step) % step;
                        var week = remainder == 0 ? afterWeek : afterWeek.AddDays((step - remainder) * 7);

                        // Two passes at most: the current qualifying week may already be spent.
                        for (var attempt = 0; attempt < 2; attempt++)
                        {
                            var candidate = EarliestInWeek(plan.Days, week, timeOfDay, afterLocal);
                            if (candidate.HasValue) return ToUtc(candidate.Value, offset);
                            week = week.AddDays(7 * step);
                        }

                        return null;
                    }

                case RepeatType.Monthly:
                    {
                        if (!plan.Date.HasValue) return null;

                        var dayOfMonth = plan.Date.Value.Day;
                        var anchorMonth = new DateTime(anchorLocal.Year, anchorLocal.Month, 1);
                        var monthsElapsed = ((afterLocal.Year - anchorLocal.Year) * 12)
                            + afterLocal.Month - anchorLocal.Month;

                        // First multiple of N that is not behind us; +1 covers a candidate that
                        // lands earlier in the month than "after".
                        var blocks = monthsElapsed <= 0 ? 0 : (monthsElapsed + step - 1) / step;

                        for (var attempt = 0; attempt < 2; attempt++)
                        {
                            var month = anchorMonth.AddMonths(blocks * step);
                            var candidate = BuildMonthly(month.Year, month.Month, dayOfMonth, timeOfDay);
                            if (candidate > afterLocal) return ToUtc(candidate, offset);
                            blocks++;
                        }

                        return null;
                    }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Next occurrence counted from when the task was actually carried out. Null when the
        /// rule does not repeat. The rule's time of day is reapplied so a vaccination confirmed
        /// at 23:40 does not start notifying at 23:40.
        /// </summary>
        public static DateTime? NextFromCompletion(SchedulePlan plan, DateTime performedAtUtc)
        {
            if (plan.RepeatType == RepeatType.Once) return null;

            var offset = plan.UtcOffsetMinutes;
            var performedLocal = ToLocal(performedAtUtc, offset);
            var timeOfDay = LocalTimeOfDay(plan.TimeOfDayUtc, offset);
            var step = Math.Max(1, plan.IntervalN);

            var baseDate = plan.RepeatType switch
            {
                RepeatType.Daily => performedLocal.Date.AddDays(step),
                RepeatType.Weekly => performedLocal.Date.AddDays(7 * step),
                // AddMonths already clamps, so 31 January + 1 month lands on 28/29 February.
                RepeatType.Monthly => performedLocal.Date.AddMonths(step),
                _ => (DateTime?)null
            };

            if (baseDate is not { } localDate) return null;

            var candidate = localDate + timeOfDay;

            if (plan.RecalcStrategy == RecalcStrategy.FromCompletionAlignedToWeekday)
                candidate = SnapForwardToSelectedDay(candidate, plan.Days);

            return ToUtc(candidate, offset);
        }

        /// <summary>
        /// Where an occurrence goes when it fired and nobody confirmed it. Calendar rules roll
        /// on as usual; the completion-driven ones must not skip a whole interval, because
        /// silently rescheduling a missed antiparasitic is the failure this whole flow exists
        /// to prevent.
        /// </summary>
        public static DateTime? NextAfterMiss(SchedulePlan plan, DateTime afterUtc)
        {
            if (plan.RepeatType == RepeatType.Once) return null;

            switch (plan.RecalcStrategy)
            {
                case RecalcStrategy.Calendar:
                    return NextFromCalendar(plan, afterUtc);

                case RecalcStrategy.FromCompletionAlignedToWeekday when plan.Days.Length > 0:
                    {
                        // Re-offer on the next selected weekday rather than a month later.
                        var offset = plan.UtcOffsetMinutes;
                        var afterLocal = ToLocal(afterUtc, offset);
                        var timeOfDay = LocalTimeOfDay(plan.TimeOfDayUtc, offset);

                        var week = WeekStart(afterLocal.Date);
                        for (var attempt = 0; attempt < 2; attempt++)
                        {
                            var candidate = EarliestInWeek(plan.Days, week, timeOfDay, afterLocal);
                            if (candidate.HasValue) return ToUtc(candidate.Value, offset);
                            week = week.AddDays(7);
                        }

                        return ToUtc(afterLocal + NagStep, offset);
                    }

                default:
                    return afterUtc + NagStep;
            }
        }

        /// <summary>
        /// First trigger for a brand new rule: the earliest matching slot from now, ignoring
        /// the interval. The interval only starts counting once that first slot becomes the
        /// anchor, which is what "every 2 weeks starting this Saturday" means.
        /// </summary>
        public static DateTime? FirstTrigger(SchedulePlan plan, DateTime nowUtc) =>
            NextFromCalendar(plan with { IntervalN = 1, AnchorUtc = nowUtc }, nowUtc);

        public static DateTime ToLocal(DateTime utc, int offsetMinutes) =>
            DateTime.SpecifyKind(utc.AddMinutes(offsetMinutes), DateTimeKind.Unspecified);

        public static DateTime ToUtc(DateTime local, int offsetMinutes) =>
            DateTime.SpecifyKind(local.AddMinutes(-offsetMinutes), DateTimeKind.Utc);

        /// <summary>
        /// Wall-clock time of day the user picked. TimeOfDay is stored in UTC, so adding the
        /// offset back can cross midnight; only the time survives, the date shift is handled
        /// by whichever local date the caller pairs it with.
        /// </summary>
        public static TimeSpan LocalTimeOfDay(TimeSpan timeOfDayUtc, int offsetMinutes)
        {
            var minutes = ((timeOfDayUtc.TotalMinutes + offsetMinutes) % 1440 + 1440) % 1440;
            return TimeSpan.FromMinutes(minutes);
        }

        /// <summary>
        /// Moves forward to the nearest selected weekday and never backwards: stepping back
        /// would shorten the interval, which is the harm the interval is guarding against.
        /// A candidate already on a selected day does not move.
        /// </summary>
        private static DateTime SnapForwardToSelectedDay(DateTime candidateLocal, DaysOfWeek[] days)
        {
            if (days.Length == 0) return candidateLocal;

            var selected = days.Select(d => (int)d).ToHashSet();
            for (var i = 0; i < 7; i++)
            {
                var shifted = candidateLocal.AddDays(i);
                if (selected.Contains((int)shifted.DayOfWeek)) return shifted;
            }

            return candidateLocal;
        }

        private static DateTime? EarliestInWeek(
            DaysOfWeek[] days, DateTime weekStart, TimeSpan timeOfDay, DateTime afterLocal)
        {
            DateTime? best = null;

            foreach (var day in days)
            {
                var candidate = weekStart.AddDays((int)day) + timeOfDay;
                if (candidate <= afterLocal) continue;
                if (best is null || candidate < best) best = candidate;
            }

            return best;
        }

        /// <summary>Sunday-based to match <see cref="DaysOfWeek"/>, where Sunday is 0.</summary>
        private static DateTime WeekStart(DateTime date) => date.AddDays(-(int)date.DayOfWeek);

        private static DateTime StepForward(DateTime anchor, DateTime after, TimeSpan step)
        {
            if (anchor > after) return anchor;

            var steps = (after - anchor).Ticks / step.Ticks;
            return anchor.AddTicks((steps + 1) * step.Ticks);
        }

        private static DateTime BuildMonthly(int year, int month, int dayOfMonth, TimeSpan timeOfDay)
        {
            var day = Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month));
            return new DateTime(year, month, day).Add(timeOfDay);
        }
    }
}
