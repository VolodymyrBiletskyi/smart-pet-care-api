using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Repository
{
    public class ReminderRepository : IReminderRepository
    {
        private readonly AppDbContext _db;

        public ReminderRepository(AppDbContext db) => _db = db;

        public async Task<IReadOnlyList<Reminder>> GetByPetIdsAsync(IEnumerable<Guid> petIds)
        {
            return await _db.Reminders
                .AsNoTracking()
                .Where(r => petIds.Contains(r.PetId))
                .OrderBy(r => r.NextTriggerAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Reminder>> GetByPetIdAsync(Guid petId)
        {
            return await _db.Reminders
                .AsNoTracking()
                .Where(r => r.PetId == petId)
                .OrderBy(r => r.NextTriggerAt)
                .ToListAsync();
        }

        public async Task<Reminder?> GetByIdAsync(Guid id)
        {
            return await _db.Reminders
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<IReadOnlyList<Reminder>> GetDueRemindersAsync(DateTime asOf)
        {
            return await _db.Reminders
                .Where(r => r.Status == ReminderStatus.Active && r.NextTriggerAt <= asOf)
                .ToListAsync();
        }

        public async Task AddAsync(Reminder reminder) =>
            await _db.Reminders.AddAsync(reminder);

        public Task DeleteAsync(Reminder reminder)
        {
            _db.Reminders.Remove(reminder);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdAsync(Guid reminderId)
        {
            return await _db.ReminderRuns
                .AsNoTracking()
                .Where(rr => rr.ReminderId == reminderId)
                .OrderByDescending(rr => rr.ScheduledFor)
                .ToListAsync();
        }

        public async Task<ReminderRun?> GetRunByIdAsync(Guid runId)
        {
            return await _db.ReminderRuns
                .FirstOrDefaultAsync(rr => rr.Id == runId);
        }

        public async Task AddRunAsync(ReminderRun run) =>
            await _db.ReminderRuns.AddAsync(run);

        public async Task<int> SaveChangesAsync() =>
            await _db.SaveChangesAsync();
    }
}
