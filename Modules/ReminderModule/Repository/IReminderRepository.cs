using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Repository
{
    public interface IReminderRepository
    {
        Task<IReadOnlyList<Reminder>> GetByPetIdsAsync(IEnumerable<Guid> petIds);
        Task<IReadOnlyList<Reminder>> GetByPetIdAsync(Guid petId);
        Task<Reminder?> GetByIdAsync(Guid id);
        Task<IReadOnlyList<Reminder>> GetDueRemindersAsync(DateTime asOf);
        Task AddAsync(Reminder reminder);
        Task DeleteAsync(Reminder reminder);
        Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdAsync(Guid reminderId);
        Task<ReminderRun?> GetRunByIdAsync(Guid runId);
        Task AddRunAsync(ReminderRun run);
        Task<int> SaveChangesAsync();

        /// <summary>Reminders that can still produce occurrences in the requested window.</summary>
        Task<IReadOnlyList<Reminder>> GetSchedulableByPetIdsAsync(IEnumerable<Guid> petIds);

        /// <summary>Runs already stored for the given rules within a window, for merging with projections.</summary>
        Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdsAsync(
            IEnumerable<Guid> reminderIds, DateTime fromUtc, DateTime toUtc);

        /// <summary>Pet-wide execution history for a period, newest first.</summary>
        Task<IReadOnlyList<(ReminderRun Run, Reminder Reminder)>> GetRunHistoryByPetIdAsync(
            Guid petId, DateTime? fromUtc, DateTime? toUtc, ReminderType? type);

        /// <summary>
        /// The occurrence a Done tap refers to: the most recent one already due and not yet
        /// confirmed. Null when nothing has fired, which means the user is confirming early.
        /// </summary>
        Task<ReminderRun?> GetLatestOpenRunAsync(Guid reminderId, DateTime asOfUtc);

        /// <summary>Guards against the same completion being registered twice.</summary>
        Task<ReminderRun?> GetCompletedRunByPerformedAtAsync(Guid reminderId, DateTime performedAtUtc);
    }
}
