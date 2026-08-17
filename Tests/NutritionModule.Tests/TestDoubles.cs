using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
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

    // Scheduling members the nutrition tests never exercise.
    public Task<IReadOnlyList<Reminder>> GetSchedulableByPetIdsAsync(IEnumerable<Guid> petIds) =>
        Task.FromResult(Reminders);
    public Task<IReadOnlyList<ReminderRun>> GetRunsByReminderIdsAsync(
        IEnumerable<Guid> reminderIds, DateTime fromUtc, DateTime toUtc) =>
        Task.FromResult<IReadOnlyList<ReminderRun>>([]);
    public Task<IReadOnlyList<(ReminderRun Run, Reminder Reminder)>> GetRunHistoryByPetIdAsync(
        Guid petId, DateTime? fromUtc, DateTime? toUtc, Enums.ReminderType? type) =>
        Task.FromResult<IReadOnlyList<(ReminderRun, Reminder)>>([]);
    public Task<ReminderRun?> GetLatestOpenRunAsync(Guid reminderId, DateTime asOfUtc) =>
        Task.FromResult<ReminderRun?>(null);
    public Task<ReminderRun?> GetCompletedRunByPerformedAtAsync(Guid reminderId, DateTime performedAtUtc) =>
        Task.FromResult<ReminderRun?>(null);
}

internal sealed class FakePetRepository : IPetRepository
{
    public bool PetExists { get; set; } = true;

    /// <summary>Returned by <see cref="GetByIdAndUserIdAsync"/> when the pet exists.</summary>
    public Pet? Pet { get; set; }

    public Task<bool> ExistsForUserAsync(Guid id, Guid userId) => Task.FromResult(PetExists);

    public Task<IReadOnlyList<Pet>> GetByUserIdAsync(Guid userId) =>
        Task.FromResult<IReadOnlyList<Pet>>([]);
    public Task<IReadOnlyList<string?>> GetPhotoPublicIdsByUserIdAsync(Guid userId) =>
        Task.FromResult<IReadOnlyList<string?>>([]);
    public Task<Pet?> GetByIdAsync(Guid id) => Task.FromResult<Pet?>(null);
    public Task<Pet?> GetByIdAndUserIdAsync(Guid id, Guid userId) =>
        Task.FromResult(PetExists ? Pet : null);
    public Task<Pet?> GetTrackedByIdAndUserIdAsync(Guid id, Guid userId) => Task.FromResult<Pet?>(null);
    public Task<Pet> AddAsync(Pet entity) => Task.FromResult(entity);
    public Task<int> SaveChangesAsync() => Task.FromResult(0);
    public void Delete(Pet pet) { }
}

/// <summary>
/// In-memory stand-in that keeps the newest-first ordering the real repository
/// guarantees, so retention behaviour can be asserted.
/// </summary>
internal sealed class FakeNutritionAnalysisRepository : INutritionAnalysisRepository
{
    public List<NutritionAnalysis> Stored { get; } = [];
    public int SaveChangesCalls { get; private set; }

    public Task<IReadOnlyList<NutritionAnalysis>> GetRecentByPetIdAsync(Guid petId, int limit) =>
        Task.FromResult<IReadOnlyList<NutritionAnalysis>>(
            [.. Ordered(petId).Take(limit)]);

    public Task<IReadOnlyList<NutritionAnalysis>> GetTrackedByPetIdAsync(Guid petId) =>
        Task.FromResult<IReadOnlyList<NutritionAnalysis>>([.. Ordered(petId)]);

    public Task<NutritionAnalysis> AddAsync(NutritionAnalysis entity)
    {
        Stored.Add(entity);
        return Task.FromResult(entity);
    }

    public void DeleteRange(IEnumerable<NutritionAnalysis> entities)
    {
        foreach (var entity in entities.ToList())
        {
            Stored.Remove(entity);
        }
    }

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }

    private IEnumerable<NutritionAnalysis> Ordered(Guid petId) =>
        Stored.Where(a => a.PetId == petId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id);
}

/// <summary>
/// Stands in for the classifier's <c>feeding-summary</c> route. The response is
/// echoed back for whichever pet the request asked about, so tests only have to
/// state the grading they care about.
/// </summary>
internal sealed class FakeClassifierClient : IClassifierClient
{
    private readonly Exception? _exception;

    public FakeClassifierClient(
        ClassifierFeedingSummaryResponse? response = null,
        Exception? exception = null)
    {
        Response = response;
        _exception = exception;
    }

    /// <summary>When null, the request's own pet is graded with <see cref="DefaultResult"/>.</summary>
    public ClassifierFeedingSummaryResponse? Response { get; set; }

    public string Disclaimer { get; set; } = "This guidance does not replace a veterinary examination.";

    public List<ClassifierFeedingSummaryRequest> Requests { get; } = [];
    public List<ClassifierChatRequest> ChatRequests { get; } = [];

    public Task<ClassifierFeedingSummaryResponse> SummarizeFeedingAsync(
        ClassifierFeedingSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (_exception is not null)
        {
            return Task.FromException<ClassifierFeedingSummaryResponse>(_exception);
        }

        return Task.FromResult(Response ?? new ClassifierFeedingSummaryResponse
        {
            Results = [.. request.Pets.Select(pet => DefaultResult(pet.PetId))],
            Disclaimer = Disclaimer
        });
    }

    public Task<ClassifierChatResponse> ChatAsync(
        ClassifierChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ChatRequests.Add(request);
        return _exception is not null
            ? Task.FromException<ClassifierChatResponse>(_exception)
            : Task.FromResult(DefaultChat());
    }

    public static ClassifierFeedingSummaryResult DefaultResult(
        string petId,
        ClassifierFeedingStatus status = ClassifierFeedingStatus.UnderTarget) => new()
        {
            PetId = petId,
            Status = status,
            TargetCalories = 600m,
            ActualCalories = 480m,
            DeviationPct = -20m
        };

    public static ClassifierChatResponse DefaultChat(string? answer = null) => new()
    {
        Mode = ClassifierChatMode.General,
        Answer = answer ?? "Buddy came in under target today.",
        SymptomSummary = string.Empty,
        RelatedTopics = ["dog nutrition"],
        Disclaimer = "This guidance does not replace a veterinary examination."
    };
}
