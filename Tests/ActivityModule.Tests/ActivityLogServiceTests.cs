using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class ActivityLogServiceTests
{
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_PersistsManualReadingAndReturnsDto()
    {
        var repo = new FakeActivityLogRepository();
        var service = Service(repo);
        var recordedAt = DateTime.UtcNow.AddHours(-2);

        var result = await service.CreateAsync(_petId, _userId, new CreateActivityLogDto
        {
            RecordedAt = recordedAt,
            Steps = 4200,
            Location = "  Central Park  ",
            Note = "  Long morning walk  "
        });

        Assert.NotNull(repo.AddedLog);
        var added = repo.AddedLog!;
        Assert.Equal(_petId, added.PetId);
        Assert.Equal(recordedAt, added.RecordedAt);
        Assert.Equal(4200, added.Steps);
        Assert.Equal("Central Park", added.Location);
        Assert.Equal("Long morning walk", added.Note);
        Assert.Equal(ActivitySource.Manual, added.Source);
        Assert.Equal(1, repo.SaveChangesCalls);

        Assert.Equal(added.Id, result.Id);
        Assert.Equal(4200, result.Steps);
        Assert.Equal(ActivitySource.Manual, result.Source);
    }

    [Fact]
    public async Task CreateAsync_TreatsUnspecifiedKindAsUtc()
    {
        var repo = new FakeActivityLogRepository();
        var service = Service(repo);
        var recordedAt = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-1), DateTimeKind.Unspecified);

        await service.CreateAsync(_petId, _userId, Dto(recordedAt: recordedAt));

        Assert.NotNull(repo.AddedLog);
        var added = repo.AddedLog!;
        Assert.Equal(DateTimeKind.Utc, added.RecordedAt.Kind);
        Assert.Equal(recordedAt.Ticks, added.RecordedAt.Ticks);
    }

    [Fact]
    public async Task CreateAsync_AcceptsALocationOnlyNote()
    {
        var repo = new FakeActivityLogRepository();

        await Service(repo).CreateAsync(_petId, _userId, new CreateActivityLogDto
        {
            RecordedAt = DateTime.UtcNow.AddMinutes(-30),
            Location = "Backyard"
        });

        Assert.NotNull(repo.AddedLog);
        var added = repo.AddedLog!;
        Assert.Null(added.Steps);
        Assert.Equal("Backyard", added.Location);
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyReading()
    {
        var repo = new FakeActivityLogRepository();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).CreateAsync(_petId, _userId, new CreateActivityLogDto
            {
                RecordedAt = DateTime.UtcNow.AddMinutes(-5),
                Note = "   "
            }));

        Assert.Contains("At least one", ex.Message);
        Assert.Null(repo.AddedLog);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_PersistsTypeIntensityAndDuration()
    {
        var repo = new FakeActivityLogRepository();

        var result = await Service(repo).CreateAsync(_petId, _userId, new CreateActivityLogDto
        {
            RecordedAt = DateTime.UtcNow.AddHours(-1),
            Type = ActivityType.Swimming,
            Intensity = ActivityIntensity.Moderate,
            DurationMinutes = 30
        });

        Assert.NotNull(repo.AddedLog);
        var added = repo.AddedLog!;
        Assert.Equal(ActivityType.Swimming, added.Type);
        Assert.Equal(ActivityIntensity.Moderate, added.Intensity);
        Assert.Equal(30, added.DurationMinutes);

        // 30 minutes at 0.7.
        Assert.Equal(21, result.ActiveMinutes);
    }

    /// <summary>
    /// A named activity with a duration is a complete log on its own — most walks are logged
    /// from a phone with no step counter anywhere near the dog.
    /// </summary>
    [Fact]
    public async Task CreateAsync_AcceptsATypeAndDurationWithNothingElse()
    {
        var repo = new FakeActivityLogRepository();

        await Service(repo).CreateAsync(_petId, _userId, new CreateActivityLogDto
        {
            RecordedAt = DateTime.UtcNow.AddMinutes(-40),
            Type = ActivityType.Walk,
            DurationMinutes = 40
        });

        Assert.NotNull(repo.AddedLog);
        var added = repo.AddedLog!;
        Assert.Null(added.Steps);
        Assert.Null(added.Location);
        Assert.Null(added.Note);
    }

    [Theory]
    [InlineData(ActivityType.Walk, ActivityIntensity.Low)]
    [InlineData(ActivityType.Run, ActivityIntensity.High)]
    [InlineData(ActivityType.Swimming, ActivityIntensity.High)]
    [InlineData(ActivityType.Training, ActivityIntensity.Moderate)]
    [InlineData(ActivityType.Play, ActivityIntensity.Moderate)]
    [InlineData(ActivityType.Other, ActivityIntensity.Moderate)]
    [InlineData(null, ActivityIntensity.Moderate)]
    public async Task CreateAsync_DerivesIntensityFromTypeWhenOmitted(ActivityType? type, ActivityIntensity expected)
    {
        var repo = new FakeActivityLogRepository();

        await Service(repo).CreateAsync(_petId, _userId, new CreateActivityLogDto
        {
            RecordedAt = DateTime.UtcNow.AddMinutes(-20),
            Type = type,
            DurationMinutes = 20
        });

        Assert.NotNull(repo.AddedLog);
        Assert.Equal(expected, repo.AddedLog!.Intensity);
    }

    [Fact]
    public async Task CreateAsync_KeepsAnExplicitIntensityOverTheDefault()
    {
        var repo = new FakeActivityLogRepository();

        await Service(repo).CreateAsync(_petId, _userId, new CreateActivityLogDto
        {
            RecordedAt = DateTime.UtcNow.AddMinutes(-25),
            Type = ActivityType.Walk,
            Intensity = ActivityIntensity.High,
            DurationMinutes = 25
        });

        Assert.NotNull(repo.AddedLog);
        Assert.Equal(ActivityIntensity.High, repo.AddedLog!.Intensity);
    }

    /// <summary>
    /// Nothing to weight means nothing to guess at: a type with no duration keeps a null
    /// intensity rather than acquiring one the caller never stated.
    /// </summary>
    [Fact]
    public async Task CreateAsync_LeavesIntensityNullWithoutADuration()
    {
        var repo = new FakeActivityLogRepository();

        var result = await Service(repo).CreateAsync(_petId, _userId, new CreateActivityLogDto
        {
            RecordedAt = DateTime.UtcNow.AddMinutes(-15),
            Type = ActivityType.Play
        });

        Assert.NotNull(repo.AddedLog);
        Assert.Null(repo.AddedLog!.Intensity);
        Assert.Null(result.ActiveMinutes);
    }

    [Theory]
    [InlineData(0, "greater than zero")]
    [InlineData(-5, "greater than zero")]
    [InlineData(1441, "1440 or less")]
    public async Task CreateAsync_RejectsDurationOutsideRange(int duration, string expectedFragment)
    {
        var repo = new FakeActivityLogRepository();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).CreateAsync(_petId, _userId, Dto(durationMinutes: duration)));

        Assert.Contains(expectedFragment, ex.Message);
        Assert.Null(repo.AddedLog);
    }

    [Fact]
    public async Task CreateAsync_RejectsUndefinedTypeAndIntensity()
    {
        var repo = new FakeActivityLogRepository();
        var service = Service(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(_petId, _userId, Dto(type: (ActivityType)42)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(_petId, _userId, Dto(intensity: (ActivityIntensity)42)));

        Assert.Null(repo.AddedLog);
    }

    [Theory]
    [InlineData(-1, "negative")]
    [InlineData(1_000_001, "1000000 or less")]
    public async Task CreateAsync_RejectsStepsOutsideRange(int steps, string expectedFragment)
    {
        var repo = new FakeActivityLogRepository();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).CreateAsync(_petId, _userId, Dto(steps: steps)));

        Assert.Contains(expectedFragment, ex.Message);
        Assert.Null(repo.AddedLog);
    }

    [Fact]
    public async Task CreateAsync_RejectsFutureRecordedAt()
    {
        var repo = new FakeActivityLogRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).CreateAsync(_petId, _userId, Dto(recordedAt: DateTime.UtcNow.AddHours(1))));

        Assert.Null(repo.AddedLog);
    }

    [Fact]
    public async Task CreateAsync_RejectsOverlongLocationAndNote()
    {
        var repo = new FakeActivityLogRepository();
        var service = Service(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(_petId, _userId, Dto(location: new string('x', 201))));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(_petId, _userId, Dto(note: new string('x', 2001))));

        Assert.Null(repo.AddedLog);
    }

    [Fact]
    public async Task CreateAsync_RejectsSourceWithoutAProvider()
    {
        var repo = new FakeActivityLogRepository();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).CreateAsync(_petId, _userId, Dto(source: ActivitySource.Device)));

        Assert.Contains("not supported yet", ex.Message);
        Assert.Null(repo.AddedLog);
    }

    [Fact]
    public async Task CreateAsync_RejectsUndefinedSource()
    {
        var repo = new FakeActivityLogRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).CreateAsync(_petId, _userId, Dto(source: (ActivitySource)99)));

        Assert.Null(repo.AddedLog);
    }

    /// <summary>
    /// The point of <see cref="IActivitySourceProvider"/>: swapping where the numbers come
    /// from changes nothing about ownership, persistence or mapping.
    /// </summary>
    [Fact]
    public async Task CreateAsync_PersistsWhateverTheRegisteredProviderReturns()
    {
        var repo = new FakeActivityLogRepository();
        var deviceRecordedAt = DateTime.UtcNow.AddHours(-3);
        var device = new StubActivitySourceProvider(
            ActivitySource.Device,
            new ActivityReading(deviceRecordedAt, 9001, "Collar GPS", "auto-captured"));
        var service = new ActivityLogService(repo, new ActivitySourceResolver([device]));

        // Request body carries manual numbers; the device provider overrides all of them.
        var result = await service.CreateAsync(_petId, _userId, new CreateActivityLogDto
        {
            RecordedAt = DateTime.UtcNow.AddDays(-1),
            Steps = 1,
            Location = "typed by hand",
            Source = ActivitySource.Device
        });

        Assert.Equal(1, device.Calls);
        Assert.Equal(_petId, device.RequestedPetId);

        Assert.NotNull(repo.AddedLog);
        var added = repo.AddedLog!;
        Assert.Equal(deviceRecordedAt, added.RecordedAt);
        Assert.Equal(9001, added.Steps);
        Assert.Equal("Collar GPS", added.Location);
        Assert.Equal("auto-captured", added.Note);
        Assert.Equal(ActivitySource.Device, added.Source);
        Assert.Equal(1, repo.SaveChangesCalls);
        Assert.Equal(ActivitySource.Device, result.Source);
    }

    [Fact]
    public async Task CreateAsync_ValidatesProviderOutputNotJustTheRequestBody()
    {
        var repo = new FakeActivityLogRepository();
        var device = new StubActivitySourceProvider(
            ActivitySource.Device,
            new ActivityReading(DateTime.UtcNow.AddHours(-1), -5, null, null));
        var service = new ActivityLogService(repo, new ActivitySourceResolver([device]));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(_petId, _userId, Dto(steps: 100, source: ActivitySource.Device)));

        Assert.Contains("negative", ex.Message);
        Assert.Null(repo.AddedLog);
    }

    [Fact]
    public async Task GetByPetIdAsync_NormalizesRangeAndMapsResults()
    {
        var log = NewLog();
        var repo = new FakeActivityLogRepository { Logs = [log] };
        var from = DateTime.SpecifyKind(new DateTime(2026, 8, 1, 0, 0, 0), DateTimeKind.Unspecified);
        var to = DateTime.SpecifyKind(new DateTime(2026, 8, 20, 0, 0, 0), DateTimeKind.Unspecified);

        var result = await Service(repo).GetByPetIdAsync(_petId, _userId, from, to, ActivitySource.Manual);

        Assert.Equal(DateTimeKind.Utc, Assert.NotNull(repo.RequestedFrom).Kind);
        Assert.Equal(from.Ticks, repo.RequestedFrom!.Value.Ticks);
        Assert.Equal(to.Ticks, Assert.NotNull(repo.RequestedTo).Ticks);
        Assert.Equal(ActivitySource.Manual, repo.RequestedSource);
        Assert.Equal(log.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetByPetIdAsync_RejectsInvertedRange()
    {
        var repo = new FakeActivityLogRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).GetByPetIdAsync(_petId, _userId, DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));

        Assert.Null(repo.RequestedFrom);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDtoOnlyForTheOwningPet()
    {
        var log = NewLog();
        var repo = new FakeActivityLogRepository { Log = log };

        var found = await Service(repo).GetByIdAsync(_petId, log.Id, _userId);
        Assert.NotNull(found);
        Assert.Equal(log.Id, found!.Id);

        repo.Log = NewLog(Guid.NewGuid());
        Assert.Null(await Service(repo).GetByIdAsync(_petId, log.Id, _userId));

        repo.Log = null;
        Assert.Null(await Service(repo).GetByIdAsync(_petId, log.Id, _userId));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheLogAndSaves()
    {
        var log = NewLog();
        var repo = new FakeActivityLogRepository { TrackedLog = log };

        Assert.True(await Service(repo).DeleteAsync(_petId, log.Id, _userId));
        Assert.Same(log, repo.DeletedLog);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForMissingOrForeignLog()
    {
        var repo = new FakeActivityLogRepository { TrackedLog = null };
        Assert.False(await Service(repo).DeleteAsync(_petId, Guid.NewGuid(), _userId));

        repo.TrackedLog = NewLog(Guid.NewGuid());
        Assert.False(await Service(repo).DeleteAsync(_petId, repo.TrackedLog.Id, _userId));

        Assert.Null(repo.DeletedLog);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task EveryOperation_RejectsAPetTheUserDoesNotOwn()
    {
        var repo = new FakeActivityLogRepository { PetBelongsToUser = false, TrackedLog = NewLog(), Log = NewLog() };
        var service = Service(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetByPetIdAsync(_petId, _userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetByIdAsync(_petId, Guid.NewGuid(), _userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(_petId, _userId, Dto()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(_petId, Guid.NewGuid(), _userId));

        Assert.Null(repo.AddedLog);
        Assert.Null(repo.DeletedLog);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    private static ActivityLogService Service(FakeActivityLogRepository repo) =>
        new(repo, new ActivitySourceResolver([new ManualActivitySourceProvider()]));

    private static CreateActivityLogDto Dto(
        DateTime? recordedAt = null,
        int? steps = 1000,
        string? location = null,
        string? note = null,
        ActivitySource? source = null,
        ActivityType? type = null,
        ActivityIntensity? intensity = null,
        int? durationMinutes = null) => new()
    {
        RecordedAt = recordedAt ?? DateTime.UtcNow.AddHours(-1),
        Steps = steps,
        Location = location,
        Note = note,
        Source = source,
        Type = type,
        Intensity = intensity,
        DurationMinutes = durationMinutes
    };

    private ActivityLog NewLog(Guid? petId = null) => new()
    {
        Id = Guid.NewGuid(),
        PetId = petId ?? _petId,
        RecordedAt = DateTime.UtcNow.AddHours(-4),
        Steps = 3000
    };
}
