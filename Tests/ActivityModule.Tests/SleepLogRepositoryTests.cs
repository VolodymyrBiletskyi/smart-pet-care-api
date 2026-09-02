using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Repository;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class SleepLogRepositoryTests
{
    [Fact]
    public async Task PetBelongsToUserAsync_RequiresMatchingPetAndUser()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        var pet = NewPet(userId);
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new SleepLogRepository(db);

        Assert.True(await repo.PetBelongsToUserAsync(pet.Id, userId));
        Assert.False(await repo.PetBelongsToUserAsync(pet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByPetIdAsync_FiltersByPetAndInclusiveDateRangeNewestFirst()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var otherPet = NewPet(Guid.NewGuid());
        db.Pets.AddRange(pet, otherPet);
        var from = Day(-5);
        var to = Day(-1);
        var oldest = NewLog(pet.Id, from);
        var newest = NewLog(pet.Id, to);
        db.SleepLogs.AddRange(
            NewLog(pet.Id, from.AddDays(-1)),
            oldest,
            newest,
            NewLog(pet.Id, to.AddDays(1)),
            NewLog(otherPet.Id, to));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new SleepLogRepository(db);

        var result = await repo.GetByPetIdAsync(pet.Id, from, to);

        Assert.Equal([newest.Id, oldest.Id], result.Select(x => x.Id));
    }

    [Fact]
    public async Task GetLoggedHoursAsync_SumsOneDayForOnePet()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var otherPet = NewPet(Guid.NewGuid());
        db.Pets.AddRange(pet, otherPet);
        var day = Day(-1);
        db.SleepLogs.AddRange(
            NewLog(pet.Id, day, 6.5m),
            NewLog(pet.Id, day, 3.25m),
            NewLog(pet.Id, Day(-2), 12m),
            NewLog(otherPet.Id, day, 9m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new SleepLogRepository(db);

        Assert.Equal(9.75m, await repo.GetLoggedHoursAsync(pet.Id, day));
    }

    /// <summary>
    /// A day nobody has logged sums to zero rather than throwing, so the first entry of the
    /// day goes through the same cap check as the second.
    /// </summary>
    [Fact]
    public async Task GetLoggedHoursAsync_ReturnsZeroForAnEmptyDay()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0m, await new SleepLogRepository(db).GetLoggedHoursAsync(pet.Id, Day(-1)));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDetachedEntityAndGetTrackedByIdAsyncTracks()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var log = NewLog(pet.Id, Day(-1));
        db.AddRange(pet, log);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var repo = new SleepLogRepository(db);

        Assert.NotNull(await repo.GetByIdAsync(log.Id));
        Assert.Empty(db.ChangeTracker.Entries<SleepLog>());

        Assert.NotNull(await repo.GetTrackedByIdAsync(log.Id));
        Assert.Single(db.ChangeTracker.Entries<SleepLog>());

        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AddDeleteAndSaveChangesAsync_PersistExpectedState()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new SleepLogRepository(db);
        var log = NewLog(pet.Id, Day(-1));

        var added = await repo.AddAsync(log);
        var addedCount = await repo.SaveChangesAsync();
        repo.Delete(log);
        var deletedCount = await repo.SaveChangesAsync();

        Assert.Same(log, added);
        Assert.Equal(1, addedCount);
        Assert.Equal(1, deletedCount);
        Assert.False(await db.SleepLogs.AnyAsync(x => x.Id == log.Id, TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DateTime Day(int offset) =>
        DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(offset), DateTimeKind.Utc);

    private static Pet NewPet(Guid userId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Name = "Pet", Species = AnimalSpecies.Dog
    };

    private static SleepLog NewLog(Guid petId, DateTime sleepDate, decimal hours = 11m) => new()
    {
        Id = Guid.NewGuid(),
        PetId = petId,
        SleepDate = sleepDate,
        Hours = hours,
        Source = ActivitySource.Manual
    };
}
