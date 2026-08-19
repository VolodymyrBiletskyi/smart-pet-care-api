using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.HealthModule.Repository;
using smart_pet_care_api.Modules.PetModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Tests;

internal sealed class FakeReminderRepository : IReminderRepository
{
    public List<Reminder> Reminders { get; } = [];
    public List<ReminderRun> Runs { get; } = [];
    public int SaveChangesCalls { get; private set; }

    public Task<IReadOnlyList<Reminder>> GetByPetIdsAsync(IEnumerable<Guid> petIds)
    {
        var ids = petIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<Reminder>>(
            Reminders.Where(r => ids.Contains(r.PetId)).ToList());
    }

    public Task<IReadOnlyList<Reminder>> GetByPetIdAsync(Guid petId) =>
        Task.FromResult<IReadOnlyList<Reminder>>(Reminders.Where(r => r.PetId == petId).ToList());

    public Task<Reminder?> GetByIdAsync(Guid id) =>
        Task.FromResult(Reminders.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<Reminder>> GetDueRemindersAsync(DateTime asOf) =>
        Task.FromResult<IReadOnlyList<Reminder>>(Reminders
            .Where(r => r.Status == ReminderStatus.Active && r.NextTriggerAt <= asOf)
            .ToList());

    public Task AddAsync(Reminder reminder)
    {
        Reminders.Add(reminder);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Reminder reminder)
    {
        Reminders.Remove(reminder);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdAsync(Guid reminderId) =>
        Task.FromResult<IReadOnlyList<ReminderRun>>(
            Runs.Where(r => r.ReminderId == reminderId).ToList());

    public Task<ReminderRun?> GetRunByIdAsync(Guid runId) =>
        Task.FromResult(Runs.FirstOrDefault(r => r.Id == runId));

    public Task AddRunAsync(ReminderRun run)
    {
        Runs.Add(run);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<Reminder>> GetSchedulableByPetIdsAsync(IEnumerable<Guid> petIds)
    {
        var ids = petIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<Reminder>>(Reminders
            .Where(r => ids.Contains(r.PetId) && r.Status == ReminderStatus.Active)
            .ToList());
    }

    public Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdsAsync(
        IEnumerable<Guid> reminderIds, DateTime fromUtc, DateTime toUtc)
    {
        var ids = reminderIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<ReminderRun>>(Runs
            .Where(r => ids.Contains(r.ReminderId)
                && r.ScheduledFor >= fromUtc
                && r.ScheduledFor <= toUtc)
            .ToList());
    }

    public Task<IReadOnlyList<(ReminderRun Run, Reminder Reminder)>> GetRunHistoryByPetIdAsync(
        Guid petId, DateTime? fromUtc, DateTime? toUtc, ReminderType? type)
    {
        var rows =
            from run in Runs
            join reminder in Reminders on run.ReminderId equals reminder.Id
            where reminder.PetId == petId
            select (Run: run, Reminder: reminder);

        if (fromUtc.HasValue)
            rows = rows.Where(x => (x.Run.PerformedAt ?? x.Run.ScheduledFor) >= fromUtc.Value);
        if (toUtc.HasValue)
            rows = rows.Where(x => (x.Run.PerformedAt ?? x.Run.ScheduledFor) <= toUtc.Value);
        if (type.HasValue)
            rows = rows.Where(x => (x.Run.Type ?? x.Reminder.Type) == type.Value);

        return Task.FromResult<IReadOnlyList<(ReminderRun, Reminder)>>(rows
            .OrderByDescending(x => x.Run.PerformedAt ?? x.Run.ScheduledFor)
            .ToList());
    }

    public Task<ReminderRun?> GetLatestOpenRunAsync(Guid reminderId, DateTime asOfUtc) =>
        Task.FromResult(Runs
            .Where(r => r.ReminderId == reminderId
                && r.ScheduledFor <= asOfUtc
                && r.Status != ReminderRunStatus.Completed
                && r.Status != ReminderRunStatus.Cancelled)
            .OrderByDescending(r => r.ScheduledFor)
            .FirstOrDefault());

    public Task<IReadOnlyList<ReminderRun>> GetUnconfirmedRunsAsync(Guid reminderId, DateTime beforeUtc) =>
        Task.FromResult<IReadOnlyList<ReminderRun>>(Runs
            .Where(r => r.ReminderId == reminderId
                && r.ScheduledFor < beforeUtc
                && r.CompletedAt == null
                && r.Status is ReminderRunStatus.Pending
                    or ReminderRunStatus.Sent
                    or ReminderRunStatus.Delivered)
            .ToList());

    public Task<ReminderRun?> GetCompletedRunInRangeAsync(Guid reminderId, DateTime fromUtc, DateTime toUtc) =>
        Task.FromResult(Runs
            .Where(r => r.ReminderId == reminderId
                && r.Status == ReminderRunStatus.Completed
                && r.PerformedAt >= fromUtc
                && r.PerformedAt < toUtc)
            .OrderByDescending(r => r.PerformedAt)
            .FirstOrDefault());
}

internal sealed class FakePetRepository : IPetRepository
{
    public bool Exists { get; set; } = true;
    public List<Pet> Pets { get; } = [];

    public Task<IReadOnlyList<Pet>> GetByUserIdAsync(Guid userId) =>
        Task.FromResult<IReadOnlyList<Pet>>(Pets);

    public Task<IReadOnlyList<string?>> GetPhotoPublicIdsByUserIdAsync(Guid userId) =>
        Task.FromResult<IReadOnlyList<string?>>([]);

    public Task<Pet?> GetByIdAsync(Guid id) => Task.FromResult(Pets.FirstOrDefault(p => p.Id == id));

    public Task<Pet?> GetByIdAndUserIdAsync(Guid id, Guid userId) =>
        Task.FromResult(Exists ? Pets.FirstOrDefault(p => p.Id == id) ?? new Pet { Id = id } : null);

    public Task<Pet?> GetTrackedByIdAndUserIdAsync(Guid id, Guid userId) => GetByIdAndUserIdAsync(id, userId);

    public Task<bool> ExistsForUserAsync(Guid id, Guid userId) => Task.FromResult(Exists);

    public Task<Pet> AddAsync(Pet entity)
    {
        Pets.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<int> SaveChangesAsync() => Task.FromResult(0);

    public void Delete(Pet pet) => Pets.Remove(pet);
}

internal sealed class FakeHealthRecordRepository : IHealthRecordRepository
{
    public List<HealthRecord> Records { get; } = [];
    public int SaveChangesCalls { get; private set; }

    public Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId) => Task.FromResult(true);

    public Task<IReadOnlyList<HealthRecord>> GetByPetIdAsync(
        Guid petId, HealthRecordType? type, SymptomType? symptom, DateTime? from, DateTime? to) =>
        Task.FromResult<IReadOnlyList<HealthRecord>>(Records.Where(r => r.PetId == petId).ToList());

    public Task<HealthRecord?> GetByIdAsync(Guid id) =>
        Task.FromResult(Records.FirstOrDefault(r => r.Id == id));

    public Task<HealthRecord?> GetTrackedByIdAsync(Guid id) => GetByIdAsync(id);

    public Task<HealthRecord?> GetByReminderAndPerformedAtAsync(Guid reminderId, DateTime performedAtUtc) =>
        Task.FromResult(Records.FirstOrDefault(r =>
            r.ReminderId == reminderId && r.PerformedAt == performedAtUtc));

    public Task<HealthRecord> AddAsync(HealthRecord entity)
    {
        Records.Add(entity);
        return Task.FromResult(entity);
    }

    public void Delete(HealthRecord entity) => Records.Remove(entity);

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(0);
    }
}
