using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using smart_pet_care_api.Modules.ActivityModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

internal sealed class FakeActivityLogRepository : IActivityLogRepository
{
    public bool PetBelongsToUser { get; set; } = true;
    public IReadOnlyList<ActivityLog> Logs { get; set; } = [];
    public ActivityLog? Log { get; set; }
    public ActivityLog? TrackedLog { get; set; }
    public ActivityLog? AddedLog { get; private set; }
    public ActivityLog? DeletedLog { get; private set; }
    public int SaveChangesCalls { get; private set; }
    public DateTime? RequestedFrom { get; private set; }
    public DateTime? RequestedTo { get; private set; }
    public ActivitySource? RequestedSource { get; private set; }

    public Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId) => Task.FromResult(PetBelongsToUser);

    public Task<IReadOnlyList<ActivityLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null, ActivitySource? source = null)
    {
        RequestedFrom = from;
        RequestedTo = to;
        RequestedSource = source;
        return Task.FromResult(Logs);
    }

    public Task<ActivityLog?> GetByIdAsync(Guid id) => Task.FromResult(Log);

    public Task<ActivityLog?> GetTrackedByIdAsync(Guid id) => Task.FromResult(TrackedLog);

    public Task<ActivityLog> AddAsync(ActivityLog entity)
    {
        AddedLog = entity;
        return Task.FromResult(entity);
    }

    public void Delete(ActivityLog entity) => DeletedLog = entity;

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeActivityLogService : IActivityLogService
{
    public Func<Guid, Guid, DateTime?, DateTime?, ActivitySource?, Task<IReadOnlyList<ActivityLogResponseDto>>> GetByPetId { get; set; } =
        (_, _, _, _, _) => Task.FromResult<IReadOnlyList<ActivityLogResponseDto>>([]);
    public Func<Guid, Guid, Guid, Task<ActivityLogResponseDto?>> GetById { get; set; } =
        (_, _, _) => Task.FromResult<ActivityLogResponseDto?>(new ActivityLogResponseDto());
    public Func<Guid, Guid, CreateActivityLogDto, Task<ActivityLogResponseDto>> Create { get; set; } =
        (_, _, _) => Task.FromResult(new ActivityLogResponseDto());
    public Func<Guid, Guid, Guid, Task<bool>> Delete { get; set; } = (_, _, _) => Task.FromResult(true);

    public Task<IReadOnlyList<ActivityLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null, ActivitySource? source = null) =>
        GetByPetId(petId, userId, from, to, source);
    public Task<ActivityLogResponseDto?> GetByIdAsync(Guid petId, Guid activityLogId, Guid userId) =>
        GetById(petId, activityLogId, userId);
    public Task<ActivityLogResponseDto> CreateAsync(Guid petId, Guid userId, CreateActivityLogDto dto) =>
        Create(petId, userId, dto);
    public Task<bool> DeleteAsync(Guid petId, Guid activityLogId, Guid userId) =>
        Delete(petId, activityLogId, userId);
}

internal sealed class FakeSleepLogRepository : ISleepLogRepository
{
    public bool PetBelongsToUser { get; set; } = true;
    public IReadOnlyList<SleepLog> Logs { get; set; } = [];
    public SleepLog? Log { get; set; }
    public SleepLog? TrackedLog { get; set; }
    public SleepLog? AddedLog { get; private set; }
    public SleepLog? DeletedLog { get; private set; }
    public decimal LoggedHours { get; set; }
    public int SaveChangesCalls { get; private set; }
    public DateTime? RequestedFrom { get; private set; }
    public DateTime? RequestedTo { get; private set; }
    public DateTime? RequestedHoursDate { get; private set; }

    public Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId) => Task.FromResult(PetBelongsToUser);

    public Task<IReadOnlyList<SleepLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null)
    {
        RequestedFrom = from;
        RequestedTo = to;
        return Task.FromResult(Logs);
    }

    public Task<SleepLog?> GetByIdAsync(Guid id) => Task.FromResult(Log);

    public Task<SleepLog?> GetTrackedByIdAsync(Guid id) => Task.FromResult(TrackedLog);

    public Task<decimal> GetLoggedHoursAsync(Guid petId, DateTime sleepDate)
    {
        RequestedHoursDate = sleepDate;
        return Task.FromResult(LoggedHours);
    }

    public Task<SleepLog> AddAsync(SleepLog entity)
    {
        AddedLog = entity;
        return Task.FromResult(entity);
    }

    public void Delete(SleepLog entity) => DeletedLog = entity;

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeSleepLogService : ISleepLogService
{
    public Func<Guid, Guid, DateTime?, DateTime?, Task<IReadOnlyList<SleepLogResponseDto>>> GetByPetId { get; set; } =
        (_, _, _, _) => Task.FromResult<IReadOnlyList<SleepLogResponseDto>>([]);
    public Func<Guid, Guid, Guid, Task<SleepLogResponseDto?>> GetById { get; set; } =
        (_, _, _) => Task.FromResult<SleepLogResponseDto?>(new SleepLogResponseDto());
    public Func<Guid, Guid, CreateSleepLogDto, Task<SleepLogResponseDto>> Create { get; set; } =
        (_, _, _) => Task.FromResult(new SleepLogResponseDto());
    public Func<Guid, Guid, Guid, Task<bool>> Delete { get; set; } = (_, _, _) => Task.FromResult(true);

    public Task<IReadOnlyList<SleepLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null) =>
        GetByPetId(petId, userId, from, to);
    public Task<SleepLogResponseDto?> GetByIdAsync(Guid petId, Guid sleepLogId, Guid userId) =>
        GetById(petId, sleepLogId, userId);
    public Task<SleepLogResponseDto> CreateAsync(Guid petId, Guid userId, CreateSleepLogDto dto) =>
        Create(petId, userId, dto);
    public Task<bool> DeleteAsync(Guid petId, Guid sleepLogId, Guid userId) =>
        Delete(petId, sleepLogId, userId);
}

/// <summary>
/// Stands in for a future device integration: it ignores the request body and answers with
/// whatever reading the test hands it, which is exactly what a collar API call would do.
/// </summary>
internal sealed class StubActivitySourceProvider : IActivitySourceProvider
{
    public StubActivitySourceProvider(ActivitySource source, ActivityReading reading)
    {
        Source = source;
        Reading = reading;
    }

    public ActivitySource Source { get; }
    public ActivityReading Reading { get; set; }
    public int Calls { get; private set; }
    public Guid? RequestedPetId { get; private set; }
    public CreateActivityLogDto? RequestedDto { get; private set; }

    public Task<ActivityReading> ReadAsync(Guid petId, CreateActivityLogDto dto)
    {
        Calls++;
        RequestedPetId = petId;
        RequestedDto = dto;
        return Task.FromResult(Reading);
    }
}
