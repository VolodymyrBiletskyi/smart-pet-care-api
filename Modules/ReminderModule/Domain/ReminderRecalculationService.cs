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

            // The same completion can legitimately arrive twice: the user taps Done on the push
            // and then saves the feeding log or a health record carrying the same reminderId.
            // The second call brings its own timestamp, so matching on the exact instant would
            // miss it — and missing it is not merely a duplicate history entry, it materialises
            // the *next* occurrence and closes that too, skipping a slot.
            var duplicate = await FindSameDayCompletionAsync(reminder, performedAt);
            if (duplicate is not null)
                return new ReminderCompletionOutcome(reminder, duplicate, AlreadyRecorded: true);

            var run = await _reminderRepo.GetLatestOpenRunAsync(reminderId, now)
                ?? await MaterialiseEarlyRunAsync(reminder, performedAt);

            run.Status = ReminderRunStatus.Completed;
            run.CompletedAt = now;
            run.PerformedAt = performedAt;
            run.Type ??= reminder.Type;
            if (!string.IsNullOrWhiteSpace(note)) run.Note = note.Trim();
            run.UpdatedAt = now;

            ApplyCompletionToSchedule(reminder, performedAt, now);

            await _reminderRepo.SaveChangesAsync();

            return new ReminderCompletionOutcome(reminder, run, AlreadyRecorded: false);
        }

        /// <summary>
        /// A completion already stored for the same local day as <paramref name="performedAt"/>.
        ///
        /// The day is the right unit because no rule this calculator can express fires twice in
        /// one: Daily lands on one time of day, Weekly on one time per selected weekday, Monthly
        /// once. So two completions sharing a day cannot be two different occurrences, while a
        /// tolerance measured in minutes would let an unhurried user slip past it. Backdating an
        /// older occurrence still works — that lands on its own day.
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
        /// Nothing has fired yet — the user is confirming ahead of the notification, which is
        /// allowed. The slot they are closing is the pending trigger, so it gets materialised
        /// now. The unique index on (ReminderId, ScheduledFor) is what keeps this from racing
        /// the scheduler; the trigger moves on in the same save, so that slot cannot fire again.
        /// </summary>
        private async Task<ReminderRun> MaterialiseEarlyRunAsync(Reminder reminder, DateTime performedAt)
        {
            var run = new ReminderRun
            {
                ReminderId = reminder.Id,
                ScheduledFor = reminder.NextTriggerAt ?? performedAt,
                Type = reminder.Type,
                Status = ReminderRunStatus.Pending
            };

            await _reminderRepo.AddRunAsync(run);
            return run;
        }

        private static void ApplyCompletionToSchedule(Reminder reminder, DateTime performedAt, DateTime now)
        {
            reminder.LastCompletedAt = performedAt;
            reminder.OverdueSince = null;
            reminder.UpdatedAt = now;

            var plan = ReminderScheduleCalculator.PlanFor(reminder);

            DateTime? next;
            if (reminder.RecalcStrategy == RecalcStrategy.Calendar)
            {
                // Confirming a calendar rule records the fact and nothing else; a pending
                // future trigger stays exactly where the calendar put it.
                next = reminder.NextTriggerAt > now
                    ? reminder.NextTriggerAt
                    : ReminderScheduleCalculator.NextFromCalendar(plan, now);
            }
            else
            {
                next = ReminderScheduleCalculator.NextFromCompletion(plan, performedAt);

                // The interval now counts from what actually happened. Leaving the anchor on
                // StartAt would make week parity and the interval disagree after the first
                // late completion.
                if (next.HasValue) reminder.ScheduleAnchorAt = next.Value;
            }

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
