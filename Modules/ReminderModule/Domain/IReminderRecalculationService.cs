using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    /// <summary>
    /// Outcome of registering a completion. <see cref="AlreadyRecorded"/> is set when the same
    /// completion arrives twice — the schedule is unchanged, not moved a second time.
    /// </summary>
    public record ReminderCompletionOutcome(
        Reminder Reminder,
        ReminderRun Run,
        bool AlreadyRecorded);

    /// <summary>
    /// The single recalculation entry point, called both when the user taps Done and when a
    /// health record is created against a reminder. Keeping one implementation is the point:
    /// two callers are fine, two copies of the date maths would not be.
    /// </summary>
    public interface IReminderRecalculationService
    {
        /// <summary>
        /// Closes the occurrence the user is confirming and moves the reminder on from
        /// <paramref name="performedAtUtc"/>. Returns null when the reminder does not exist or
        /// does not belong to <paramref name="expectedPetId"/>. Safe to call twice with the
        /// same date: the next trigger is a function of the completion date, not an offset
        /// from the current one.
        /// </summary>
        Task<ReminderCompletionOutcome?> RegisterCompletionAsync(
            Guid reminderId, DateTime performedAtUtc, string? note = null, Guid? expectedPetId = null);
    }
}
