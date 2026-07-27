using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.FeedingModule.Repository;
using smart_pet_care_api.Modules.NutritionModule.Repository;
using smart_pet_care_api.Modules.PetModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.Repository;

namespace smart_pet_care_api.Modules.NutritionModule.Tests;

internal sealed class FakeNutritionGoalRepository : INutritionGoalRepository
{
    public NutritionGoal? Goal { get; set; }
    public NutritionGoal? AddedGoal { get; private set; }
    public NutritionGoal? DeletedGoal { get; private set; }
    public int SaveChangesCalls { get; private set; }

    public Task<NutritionGoal?> GetByPetIdAsync(Guid petId) => Task.FromResult(Goal);
    public Task<NutritionGoal?> GetTrackedByPetIdAsync(Guid petId) => Task.FromResult(Goal);

    public Task<NutritionGoal> AddAsync(NutritionGoal entity)
    {
        AddedGoal = entity;
        Goal = entity;
        return Task.FromResult(entity);
    }

    public void Delete(NutritionGoal entity) => DeletedGoal = entity;

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeFeedingLogRepository : IFeedingLogRepository
{
    public IReadOnlyList<FeedingLog> Logs { get; set; } = [];
    public Guid? RequestedPetId { get; private set; }
    public DateTime? RequestedStartUtc { get; private set; }
    public DateTime? RequestedEndUtc { get; private set; }

    public Task<IReadOnlyList<FeedingLog>> GetByPetIdAndRangeAsync(Guid petId, DateTime startUtc, DateTime endUtc)
    {
        RequestedPetId = petId;
        RequestedStartUtc = startUtc;
        RequestedEndUtc = endUtc;
        return Task.FromResult(Logs);
    }

    public Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId) => Task.FromResult(true);
    public Task<IReadOnlyList<FeedingLog>> GetByPetIdAsync(Guid petId) => Task.FromResult(Logs);
    public Task<FeedingLog?> GetByIdAsync(Guid id) => Task.FromResult<FeedingLog?>(null);
    public Task<FeedingLog?> GetTrackedByIdAsync(Guid id) => Task.FromResult<FeedingLog?>(null);
    public Task<FeedingLog> AddAsync(FeedingLog entity) => Task.FromResult(entity);
    public void Delete(FeedingLog entity) { }
    public Task<int> SaveChangesAsync() => Task.FromResult(0);
}

internal sealed class FakeReminderRepository : IReminderRepository
{
    public IReadOnlyList<Reminder> Reminders { get; set; } = [];

    public Task<IReadOnlyList<Reminder>> GetByPetIdAsync(Guid petId) => Task.FromResult(Reminders);

    public Task<IReadOnlyList<Reminder>> GetByPetIdsAsync(IEnumerable<Guid> petIds) =>
        Task.FromResult(Reminders);
    public Task<Reminder?> GetByIdAsync(Guid id) => Task.FromResult<Reminder?>(null);
    public Task<IReadOnlyList<Reminder>> GetDueRemindersAsync(DateTime asOf) =>
        Task.FromResult<IReadOnlyList<Reminder>>([]);
    public Task AddAsync(Reminder reminder) => Task.CompletedTask;
    public Task DeleteAsync(Reminder reminder) => Task.CompletedTask;
    public Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdAsync(Guid reminderId) =>
        Task.FromResult<IReadOnlyList<ReminderRun>>([]);
    public Task<ReminderRun?> GetRunByIdAsync(Guid runId) => Task.FromResult<ReminderRun?>(null);
    public Task AddRunAsync(ReminderRun run) => Task.CompletedTask;
    public Task<int> SaveChangesAsync() => Task.FromResult(0);
}

internal sealed class FakePetRepository : IPetRepository
{
    public bool PetExists { get; set; } = true;

    public Task<bool> ExistsForUserAsync(Guid id, Guid userId) => Task.FromResult(PetExists);

    public Task<IReadOnlyList<Pet>> GetByUserIdAsync(Guid userId) =>
        Task.FromResult<IReadOnlyList<Pet>>([]);
    public Task<IReadOnlyList<string?>> GetPhotoPublicIdsByUserIdAsync(Guid userId) =>
        Task.FromResult<IReadOnlyList<string?>>([]);
    public Task<Pet?> GetByIdAsync(Guid id) => Task.FromResult<Pet?>(null);
    public Task<Pet?> GetByIdAndUserIdAsync(Guid id, Guid userId) => Task.FromResult<Pet?>(null);
    public Task<Pet?> GetTrackedByIdAndUserIdAsync(Guid id, Guid userId) => Task.FromResult<Pet?>(null);
    public Task<Pet> AddAsync(Pet entity) => Task.FromResult(entity);
    public Task<int> SaveChangesAsync() => Task.FromResult(0);
    public void Delete(Pet pet) { }
}
