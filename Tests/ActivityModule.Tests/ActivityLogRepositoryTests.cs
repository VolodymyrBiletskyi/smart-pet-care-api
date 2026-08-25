using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Repository;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class ActivityLogRepositoryTests
{
    [Fact]
    public async Task PetBelongsToUserAsync_RequiresMatchingPetAndUser()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        var pet = NewPet(userId);
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityLogRepository(db);

        Assert.True(await repo.PetBelongsToUserAsync(pet.Id, userId));
        Assert.False(await repo.PetBelongsToUserAsync(pet.Id, Guid.NewGuid()));
        Assert.False(await repo.PetBelongsToUserAsync(Guid.NewGuid(), userId));
    }

    [Fact]
    public async Task GetByPetIdAsync_FiltersByPetAndInclusiveDateRangeNewestFirst()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var otherPet = NewPet(Guid.NewGuid());
        db.Pets.AddRange(pet, otherPet);
        var from = DateTime.UtcNow.AddDays(-5);
        var to = DateTime.UtcNow.AddDays(-1);
        var oldest = NewLog(pet.Id, from);
        var newest = NewLog(pet.Id, to);
        db.ActivityLogs.AddRange(
            NewLog(pet.Id, from.AddTicks(-1)),
            oldest,
            newest,
            NewLog(pet.Id, to.AddTicks(1)),
            NewLog(otherPet.Id, to));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityLogRepository(db);

        var result = await repo.GetByPetIdAsync(pet.Id, from, to);

        Assert.Equal([newest.Id, oldest.Id], result.Select(x => x.Id));
    }

    [Fact]
    public async Task GetByPetIdAsync_AppliesEachBoundIndependently()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        var older = NewLog(pet.Id, DateTime.UtcNow.AddDays(-10));
        var newer = NewLog(pet.Id, DateTime.UtcNow.AddDays(-1));
        db.ActivityLogs.AddRange(older, newer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityLogRepository(db);

        Assert.Equal([newer.Id], (await repo.GetByPetIdAsync(pet.Id, from: DateTime.UtcNow.AddDays(-5))).Select(x => x.Id));
        Assert.Equal([older.Id], (await repo.GetByPetIdAsync(pet.Id, to: DateTime.UtcNow.AddDays(-5))).Select(x => x.Id));
        Assert.Equal(2, (await repo.GetByPetIdAsync(pet.Id)).Count);
    }

    [Fact]
    public async Task GetByPetIdAsync_FiltersBySource()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        var manual = NewLog(pet.Id, DateTime.UtcNow.AddDays(-1));
        var device = NewLog(pet.Id, DateTime.UtcNow.AddDays(-2), ActivitySource.Device);
        db.ActivityLogs.AddRange(manual, device);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityLogRepository(db);

        var result = await repo.GetByPetIdAsync(pet.Id, source: ActivitySource.Device);

        Assert.Equal(device.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDetachedEntityAndGetTrackedByIdAsyncTracks()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var log = NewLog(pet.Id, DateTime.UtcNow.AddDays(-1));
        db.AddRange(pet, log);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var repo = new ActivityLogRepository(db);

        Assert.NotNull(await repo.GetByIdAsync(log.Id));
        Assert.Empty(db.ChangeTracker.Entries<ActivityLog>());

        Assert.NotNull(await repo.GetTrackedByIdAsync(log.Id));
        Assert.Single(db.ChangeTracker.Entries<ActivityLog>());

        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AddDeleteAndSaveChangesAsync_PersistExpectedState()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new ActivityLogRepository(db);
        var log = NewLog(pet.Id, DateTime.UtcNow.AddDays(-1));

        var added = await repo.AddAsync(log);
        var addedCount = await repo.SaveChangesAsync();
        repo.Delete(log);
        var deletedCount = await repo.SaveChangesAsync();

        Assert.Same(log, added);
        Assert.Equal(1, addedCount);
        Assert.Equal(1, deletedCount);
        Assert.False(await db.ActivityLogs.AnyAsync(x => x.Id == log.Id, TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Pet NewPet(Guid userId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Name = "Pet", Species = AnimalSpecies.Dog
    };

    private static ActivityLog NewLog(Guid petId, DateTime recordedAt, ActivitySource source = ActivitySource.Manual) => new()
    {
        Id = Guid.NewGuid(),
        PetId = petId,
        RecordedAt = recordedAt,
        Steps = 1500,
        Location = "Park",
        Source = source
    };
}
