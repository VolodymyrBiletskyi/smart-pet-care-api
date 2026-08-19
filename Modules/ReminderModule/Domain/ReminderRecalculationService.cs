using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ReminderModule.Mapper;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    public class ReminderRecalculationService : IReminderRecalculationService
    {
        private readonly IReminderRepository _reminderRepo;

        public ReminderRecalculationService(IReminderRepository reminderRepo)
        {
            _reminderRepo = reminderRepo;
        }

        public async Task<ReminderCompletionOutcome?> RegisterCompletionAsync(
            Guid reminderId, DateTime performedAtUtc, string? note = null, Guid? expectedPetId = null)
        {
            var reminder = await _reminderRepo.GetByIdAsync(reminderId);
            if (reminder is null) return null;
            if (expectedPetId.HasValue && reminder.PetId != expectedPetId.Value) return null;

            var now = DateTime.UtcNow;
            var performedAt = ReminderMapper.NormalizeToUtc(performedAtUtc);

            var duplicate = await FindSameDayCompletionAsync(reminder, performedAt);
            if (duplicate is not null)
                return new ReminderCompletionOutcome(reminder, duplicate, AlreadyRecorded: true);

            var nextTrigger = ComputeNextTrigger(reminder, performedAt, now);

            var run = await _reminderRepo.GetLatestOpenRunAsync(reminderId, now)
                ?? await MaterialiseEarlyRunAsync(reminder, performedAt, nextTrigger);

            run.Status = ReminderRunStatus.Completed;
            run.CompletedAt = now;
            run.PerformedAt = performedAt;
            run.Type ??= reminder.Type;
            if (!string.IsNullOrWhiteSpace(note)) run.Note = note.Trim();
            run.UpdatedAt = now;

            ApplyCompletionToSchedule(reminder, performedAt, now, nextTrigger);

            await _reminderRepo.SaveChangesAsync();

            return new ReminderCompletionOutcome(reminder, run, AlreadyRecorded: false);
        }

        /// <summary>
        /// The same completion can arrive from /complete and from a log carrying the same
        /// reminderId, each with its own timestamp. A whole local day is the window because no
        /// rule here fires twice in one, so sharing a day means sharing an occurrence.
        /// </summary>
        private async Task<ReminderRun?> FindSameDayCompletionAsync(Reminder reminder, DateTime performedAt)
        {
            var offset = reminder.UtcOffsetMinutes;
            var dayStartLocal = ReminderScheduleCalculator.ToLocal(performedAt, offset).Date;

            return await _reminderRepo.GetCompletedRunInRangeAsync(
                reminder.Id,
                ReminderScheduleCalculator.ToUtc(dayStartLocal, offset),
                ReminderScheduleCalculator.ToUtc(dayStartLocal.AddDays(1), offset));
        }

        /// <summary>
        /// Side-effect free, because <see cref="MaterialiseEarlyRunAsync"/> needs the answer
        /// before the run exists; the anchor and trigger are written later.
        /// </summary>
        private static DateTime? ComputeNextTrigger(Reminder reminder, DateTime performedAt, DateTime now)
        {
            var plan = ReminderScheduleCalculator.PlanFor(reminder);

            // Confirming a calendar rule records the fact and nothing else; a pending future
            // trigger stays exactly where the calendar put it.
            if (reminder.RecalcStrategy == RecalcStrategy.Calendar)
                return reminder.NextTriggerAt > now
                    ? reminder.NextTriggerAt
                    : ReminderScheduleCalculator.NextFromCalendar(plan, now);

            return ReminderScheduleCalculator.NextFromCompletion(plan, performedAt);
        }

        /// <summary>
        /// Nothing has fired yet, so the run is created here. It takes the pending slot only
        /// when the completion carries the schedule past it — Saturday's bath done on Thursday.
        /// A trigger that recomputes back onto the pending instant means the user did something
        /// extra today, so that slot keeps its notification and the run is filed at the time it
        /// happened; taking it would also collide with the scheduler on the unique
        /// (ReminderId, ScheduledFor) index once that instant arrived.
        /// </summary>
        private async Task<ReminderRun> MaterialiseEarlyRunAsync(
            Reminder reminder, DateTime performedAt, DateTime? nextTrigger)
        {
            var pending = reminder.NextTriggerAt;
            var closesPendingSlot = pending.HasValue && (nextTrigger is null || nextTrigger > pending);

            var run = new ReminderRun
            {
                ReminderId = reminder.Id,
                ScheduledFor = closesPendingSlot ? pending!.Value : performedAt,
                Type = reminder.Type,
                Status = ReminderRunStatus.Pending
            };

            await _reminderRepo.AddRunAsync(run);
            return run;
        }

        private static void ApplyCompletionToSchedule(
            Reminder reminder, DateTime performedAt, DateTime now, DateTime? next)
        {
            reminder.LastCompletedAt = performedAt;
            reminder.OverdueSince = null;
            reminder.UpdatedAt = now;

            // The interval now counts from what actually happened. Leaving the anchor on StartAt
            // would make week parity and the interval disagree after the first late completion.
            if (reminder.RecalcStrategy != RecalcStrategy.Calendar && next.HasValue)
                reminder.ScheduleAnchorAt = next.Value;

            if (next is null || (reminder.EndAt.HasValue && next > reminder.EndAt))
            {
                reminder.Status = ReminderStatus.Completed;
                reminder.NextTriggerAt = null;
                return;
            }

            reminder.NextTriggerAt = next;
            reminder.Status = ReminderStatus.Active;
        }
    }
}
