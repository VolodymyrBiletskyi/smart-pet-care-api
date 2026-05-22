using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.ReminderModule.Repository
{
    public interface IReminderRepository
    {
        Task<IReadOnlyList<Reminder>> GetByUserIdAsync(Guid userId);
        Task<IReadOnlyList<Reminder>> GetByPetIdAsync(Guid petId, Guid userId);
        Task<Reminder?> GetByIdAsync(Guid id, Guid userId);
        Task<IReadOnlyList<Reminder>> GetDueRemindersAsync(DateTime asOf);
        Task AddAsync(Reminder reminder);
        Task DeleteAsync(Reminder reminder);
        Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdAsync(Guid reminderId, Guid userId);
        Task<ReminderRun?> GetRunByIdAsync(Guid runId, Guid userId);
        Task AddRunAsync(ReminderRun run);
        Task<int> SaveChangesAsync();
    }
}
