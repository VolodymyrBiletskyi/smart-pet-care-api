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
        /// <param name="autoSave">
        /// Commits the closed occurrence on its own, which is what a caller with nothing else to
        /// write wants. Callers that also store a log — a weight, a portion, a health record —
        /// pass false and save once: every repository here shares the request's single
        /// AppDbContext, so one SaveChanges puts the log and the occurrence in one transaction.
        /// Committing separately meant a log that failed validation or lost an index race left
        /// the occurrence closed and the schedule advanced with nothing recorded against it.
        /// </param>
        Task<ReminderCompletionOutcome?> RegisterCompletionAsync(
            Guid reminderId, DateTime performedAtUtc, string? note = null, Guid? expectedPetId = null,
            bool autoSave = true);
    }
}
