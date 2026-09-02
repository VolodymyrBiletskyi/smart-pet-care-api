using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class SleepLogServiceTests
{
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_PersistsTheEntryAsManual()
    {
        var repo = new FakeSleepLogRepository();

        var result = await new SleepLogService(repo).CreateAsync(_petId, _userId, new CreateSleepLogDto
        {
            SleepDate = new DateTime(2026, 8, 20, 22, 15, 0),
            Hours = 12.5m,
            Note = "  Restless night  "
        });

        Assert.NotNull(repo.AddedLog);
        var added = repo.AddedLog!;
        Assert.Equal(_petId, added.PetId);
        Assert.Equal(12.5m, added.Hours);
        Assert.Equal("Restless night", added.Note);
        Assert.Equal(ActivitySource.Manual, added.Source);
        Assert.Equal(1, repo.SaveChangesCalls);
        Assert.Equal(added.Id, result.Id);
    }

    /// <summary>
    /// The date arrives as the owner's local day. Converting it would push evening entries
    /// onto the wrong date for half the world, so only the time is dropped.
    /// </summary>
    [Fact]
    public async Task CreateAsync_KeepsTheCalendarDayAndDropsTheTime()
    {
        var repo = new FakeSleepLogRepository();

        await new SleepLogService(repo).CreateAsync(_petId, _userId, Dto(
            sleepDate: DateTime.SpecifyKind(new DateTime(2026, 8, 20, 23, 45, 0), DateTimeKind.Local)));

        Assert.NotNull(repo.AddedLog);
        var added = repo.AddedLog!;
        Assert.Equal(new DateTime(2026, 8, 20), added.SleepDate);
        Assert.Equal(DateTimeKind.Utc, added.SleepDate.Kind);
    }

    [Fact]
    public async Task CreateAsync_AllowsSeveralNapsOnTheSameDay()
    {
        var repo = new FakeSleepLogRepository { LoggedHours = 6m };

        await new SleepLogService(repo).CreateAsync(_petId, _userId, Dto(hours: 4m));

        Assert.NotNull(repo.AddedLog);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    /// <summary>
    /// The cap on the day's sum is what catches the same night entered twice — the mistake a
    /// unique index would have caught, without banning nap-by-nap logging.
    /// </summary>
    [Fact]
    public async Task CreateAsync_RejectsADayTotallingOverTwentyFourHours()
    {
        var repo = new FakeSleepLogRepository { LoggedHours = 20m };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            new SleepLogService(repo).CreateAsync(_petId, _userId, Dto(hours: 5m)));

        Assert.Contains("more than 24 hours", ex.Message);
        Assert.Null(repo.AddedLog);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_CountsTheDayItIsAboutNotToday()
    {
        var repo = new FakeSleepLogRepository();
        var sleepDate = DateTime.UtcNow.AddDays(-3);

        await new SleepLogService(repo).CreateAsync(_petId, _userId, Dto(sleepDate: sleepDate));

        Assert.Equal(sleepDate.Date, Assert.NotNull(repo.RequestedHoursDate));
    }

    [Theory]
    [InlineData(0, "greater than zero")]
    [InlineData(-2, "greater than zero")]
    [InlineData(25, "24 or less")]
    public async Task CreateAsync_RejectsHoursOutsideRange(decimal hours, string expectedFragment)
    {
        var repo = new FakeSleepLogRepository();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            new SleepLogService(repo).CreateAsync(_petId, _userId, Dto(hours: hours)));

        Assert.Contains(expectedFragment, ex.Message);
        Assert.Null(repo.AddedLog);
    }

    [Fact]
    public async Task CreateAsync_RejectsAFutureDayButAcceptsToday()
    {
        var repo = new FakeSleepLogRepository();
        var service = new SleepLogService(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(_petId, _userId, Dto(sleepDate: DateTime.UtcNow.AddDays(1))));
        Assert.Null(repo.AddedLog);

        await service.CreateAsync(_petId, _userId, Dto(sleepDate: DateTime.UtcNow));
        Assert.NotNull(repo.AddedLog);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnOverlongNote()
    {
        var repo = new FakeSleepLogRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SleepLogService(repo).CreateAsync(_petId, _userId, Dto(note: new string('x', 2001))));

        Assert.Null(repo.AddedLog);
    }

    [Fact]
    public async Task GetByPetIdAsync_NormalizesTheRangeToWholeDays()
    {
        var log = NewLog();
        var repo = new FakeSleepLogRepository { Logs = [log] };

        var result = await new SleepLogService(repo).GetByPetIdAsync(
            _petId, _userId, new DateTime(2026, 8, 1, 13, 0, 0), new DateTime(2026, 8, 20, 9, 0, 0));

        Assert.Equal(new DateTime(2026, 8, 1), Assert.NotNull(repo.RequestedFrom));
        Assert.Equal(new DateTime(2026, 8, 20), Assert.NotNull(repo.RequestedTo));
        Assert.Equal(log.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetByPetIdAsync_RejectsInvertedRange()
    {
        var repo = new FakeSleepLogRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SleepLogService(repo).GetByPetIdAsync(_petId, _userId, DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));

        Assert.Null(repo.RequestedFrom);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDtoOnlyForTheOwningPet()
    {
        var log = NewLog();
        var repo = new FakeSleepLogRepository { Log = log };
        var service = new SleepLogService(repo);

        var found = await service.GetByIdAsync(_petId, log.Id, _userId);
        Assert.NotNull(found);
        Assert.Equal(log.Id, found!.Id);

        repo.Log = NewLog(Guid.NewGuid());
        Assert.Null(await service.GetByIdAsync(_petId, log.Id, _userId));

        repo.Log = null;
        Assert.Null(await service.GetByIdAsync(_petId, log.Id, _userId));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheLogThenReportsMissingOrForeignOnes()
    {
        var log = NewLog();
        var repo = new FakeSleepLogRepository { TrackedLog = log };
        var service = new SleepLogService(repo);

        Assert.True(await service.DeleteAsync(_petId, log.Id, _userId));
        Assert.Same(log, repo.DeletedLog);
        Assert.Equal(1, repo.SaveChangesCalls);

        repo.TrackedLog = null;
        Assert.False(await service.DeleteAsync(_petId, Guid.NewGuid(), _userId));

        repo.TrackedLog = NewLog(Guid.NewGuid());
        Assert.False(await service.DeleteAsync(_petId, repo.TrackedLog.Id, _userId));

        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task EveryOperation_RejectsAPetTheUserDoesNotOwn()
    {
        var repo = new FakeSleepLogRepository
        {
            PetBelongsToUser = false,
            Log = NewLog(),
            TrackedLog = NewLog()
        };
        var service = new SleepLogService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetByPetIdAsync(_petId, _userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetByIdAsync(_petId, Guid.NewGuid(), _userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(_petId, _userId, Dto()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(_petId, Guid.NewGuid(), _userId));

        Assert.Null(repo.AddedLog);
        Assert.Null(repo.DeletedLog);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    private static CreateSleepLogDto Dto(
        DateTime? sleepDate = null,
        decimal hours = 10m,
        string? note = null) => new()
    {
        SleepDate = sleepDate ?? DateTime.UtcNow.AddDays(-1),
        Hours = hours,
        Note = note
    };

    private SleepLog NewLog(Guid? petId = null) => new()
    {
        Id = Guid.NewGuid(),
        PetId = petId ?? _petId,
        SleepDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-1), DateTimeKind.Utc),
        Hours = 11m
    };
}
