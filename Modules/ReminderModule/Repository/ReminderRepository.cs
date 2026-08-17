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
                .Include(r => r.Pet)
                .Where(r => petIds.Contains(r.PetId))
                .OrderBy(r => r.NextTriggerAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Reminder>> GetByPetIdAsync(Guid petId)
        {
            return await _db.Reminders
                .AsNoTracking()
                .Include(r => r.Pet)
                .Where(r => r.PetId == petId)
                .OrderBy(r => r.NextTriggerAt)
                .ToListAsync();
        }

        public async Task<Reminder?> GetByIdAsync(Guid id)
        {
            return await _db.Reminders
                .Include(r => r.Pet)
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

        public async Task<IReadOnlyList<Reminder>> GetSchedulableByPetIdsAsync(IEnumerable<Guid> petIds)
        {
            return await _db.Reminders
                .AsNoTracking()
                .Where(r => petIds.Contains(r.PetId) && r.Status == ReminderStatus.Active)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdsAsync(
            IEnumerable<Guid> reminderIds, DateTime fromUtc, DateTime toUtc)
        {
            return await _db.ReminderRuns
                .AsNoTracking()
                .Where(rr => reminderIds.Contains(rr.ReminderId)
                    && rr.ScheduledFor >= fromUtc
                    && rr.ScheduledFor <= toUtc)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<(ReminderRun Run, Reminder Reminder)>> GetRunHistoryByPetIdAsync(
            Guid petId, DateTime? fromUtc, DateTime? toUtc, ReminderType? type)
        {
            var query =
                from run in _db.ReminderRuns.AsNoTracking()
                join reminder in _db.Reminders.AsNoTracking() on run.ReminderId equals reminder.Id
                where reminder.PetId == petId
                select new { Run = run, Reminder = reminder };

            if (fromUtc.HasValue)
                query = query.Where(x => (x.Run.PerformedAt ?? x.Run.ScheduledFor) >= fromUtc.Value);

            if (toUtc.HasValue)
                query = query.Where(x => (x.Run.PerformedAt ?? x.Run.ScheduledFor) <= toUtc.Value);

            // Falls back to the rule's current type only for runs stored before the snapshot
            // column existed.
            if (type.HasValue)
                query = query.Where(x => (x.Run.Type ?? x.Reminder.Type) == type.Value);

            var rows = await query
                .OrderByDescending(x => x.Run.PerformedAt ?? x.Run.ScheduledFor)
                .ToListAsync();

            return rows.Select(x => (x.Run, x.Reminder)).ToList();
        }

        public async Task<ReminderRun?> GetLatestOpenRunAsync(Guid reminderId, DateTime asOfUtc)
        {
            return await _db.ReminderRuns
                .Where(rr => rr.ReminderId == reminderId
                    && rr.ScheduledFor <= asOfUtc
                    && rr.Status != ReminderRunStatus.Completed
                    && rr.Status != ReminderRunStatus.Cancelled)
                .OrderByDescending(rr => rr.ScheduledFor)
                .FirstOrDefaultAsync();
        }

        public async Task<ReminderRun?> GetCompletedRunByPerformedAtAsync(Guid reminderId, DateTime performedAtUtc)
        {
            return await _db.ReminderRuns
                .Where(rr => rr.ReminderId == reminderId
                    && rr.Status == ReminderRunStatus.Completed
                    && rr.PerformedAt == performedAtUtc)
                .FirstOrDefaultAsync();
        }
    }
}
