using smart_pet_care_api.Modules.ReminderModule.Domain;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Domain;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Responses;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Repository;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Tests;

internal sealed class FakePetWeightLogRepository : IPetWeightLogRepository
{
    public bool PetBelongsToUser { get; set; } = true;
    public IReadOnlyList<PetWeightLog> Logs { get; set; } = [];
    public PetWeightLog? TrackedLog { get; set; }
    public PetWeightLog? LatestLog { get; set; }
    public Pet? TrackedPet { get; set; } = new();
    public bool MeasuredAtExists { get; set; }
    public PetWeightLog? AddedLog { get; private set; }
    public PetWeightLog? DeletedLog { get; private set; }
    public int SaveChangesCalls { get; private set; }
    public int GetByPetIdCalls { get; private set; }
    public int ExistsCalls { get; private set; }
    public DateTime? RequestedFrom { get; private set; }
    public DateTime? RequestedTo { get; private set; }
    public DateTime? CheckedMeasuredAt { get; private set; }
    public Guid? CheckedExcludeId { get; private set; }
    public Guid? LatestExcludedId { get; private set; }

    public Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId) => Task.FromResult(PetBelongsToUser);

    public Task<IReadOnlyList<PetWeightLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null)
    {
        GetByPetIdCalls++;
        RequestedFrom = from;
        RequestedTo = to;
        return Task.FromResult(Logs);
    }

    public Task<PetWeightLog?> GetTrackedByIdAsync(Guid id) => Task.FromResult(TrackedLog);
    public Task<PetWeightLog?> GetLatestByPetIdAsync(Guid petId, Guid? excludeId = null)
    {
        LatestExcludedId = excludeId;
        return Task.FromResult(LatestLog);
    }
    public Task<Pet?> GetTrackedPetByIdAsync(Guid petId) => Task.FromResult(TrackedPet);

    public Task<bool> ExistsForPetAtMeasuredAtAsync(Guid petId, DateTime measuredAt, Guid? excludeId = null)
    {
        ExistsCalls++;
        CheckedMeasuredAt = measuredAt;
        CheckedExcludeId = excludeId;
        return Task.FromResult(MeasuredAtExists);
    }

    public Task<PetWeightLog> AddAsync(PetWeightLog entity)
    {
        AddedLog = entity;
        LatestLog ??= entity;
        return Task.FromResult(entity);
    }

    public void Delete(PetWeightLog entity) => DeletedLog = entity;

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}

internal sealed class FakePetWeightLogService : IPetWeightLogService
{
    public Func<Guid, Guid, DateTime?, DateTime?, Task<IReadOnlyList<PetWeightLogResponseDto>>> GetByPetId { get; set; } =
        (_, _, _, _) => Task.FromResult<IReadOnlyList<PetWeightLogResponseDto>>([]);
    public Func<Guid, Guid, CreatePetWeightLogDto, Task<PetWeightLogResponseDto>> Create { get; set; } =
        (_, _, _) => Task.FromResult(new PetWeightLogResponseDto());
    public Func<Guid, Guid, Guid, PatchPetWeightLogDto, Task<PetWeightLogResponseDto>> Update { get; set; } =
        (_, _, _, _) => Task.FromResult(new PetWeightLogResponseDto());
    public Func<Guid, Guid, Guid, Task<bool>> Delete { get; set; } = (_, _, _) => Task.FromResult(true);

    public Task<IReadOnlyList<PetWeightLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null) =>
        GetByPetId(petId, userId, from, to);
    public Task<PetWeightLogResponseDto> CreateAsync(Guid petId, Guid userId, CreatePetWeightLogDto dto) => Create(petId, userId, dto);
    public Task<PetWeightLogResponseDto> UpdateAsync(Guid petId, Guid weightLogId, Guid userId, PatchPetWeightLogDto dto) =>
        Update(petId, weightLogId, userId, dto);
    public Task<bool> DeleteAsync(Guid petId, Guid weightLogId, Guid userId) => Delete(petId, weightLogId, userId);
}

/// <summary>
/// Records completion registrations so tests can assert the weighing reminder was closed,
/// without pulling the whole reminder module into this suite.
/// </summary>
internal sealed class FakeReminderRecalculationService : IReminderRecalculationService
{
    public Guid? RegisteredReminderId { get; private set; }
    public DateTime? RegisteredPerformedAt { get; private set; }
    public Guid? RegisteredPetId { get; private set; }
    public int Calls { get; private set; }

    /// <summary>Set to false to simulate a reminder that does not exist or belongs elsewhere.</summary>
    public bool ReminderResolves { get; set; } = true;

    public Task<ReminderCompletionOutcome?> RegisterCompletionAsync(
        Guid reminderId, DateTime performedAtUtc, string? note = null, Guid? expectedPetId = null)
    {
        Calls++;
        RegisteredReminderId = reminderId;
        RegisteredPerformedAt = performedAtUtc;
        RegisteredPetId = expectedPetId;

        if (!ReminderResolves) return Task.FromResult<ReminderCompletionOutcome?>(null);

        var reminder = new Reminder
        {
            Id = reminderId,
            PetId = expectedPetId ?? Guid.NewGuid(),
            Title = "Weigh-in",
            NextTriggerAt = performedAtUtc.AddDays(7)
        };

        var run = new ReminderRun { ReminderId = reminderId, PerformedAt = performedAtUtc };

        return Task.FromResult<ReminderCompletionOutcome?>(
            new ReminderCompletionOutcome(reminder, run, AlreadyRecorded: false));
    }
}
