using smart_pet_care_api.Models;

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
    }
}
