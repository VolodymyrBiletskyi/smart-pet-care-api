using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Repository;
using Xunit;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Tests;

public class PetWeightLogRepositoryTests
{
    [Fact]
    public async Task PetBelongsToUserAsync_RequiresMatchingPetAndUser()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        var pet = NewPet(userId);
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PetWeightLogRepository(db);

        Assert.True(await repo.PetBelongsToUserAsync(pet.Id, userId));
        Assert.False(await repo.PetBelongsToUserAsync(pet.Id, Guid.NewGuid()));
        Assert.False(await repo.PetBelongsToUserAsync(Guid.NewGuid(), userId));
    }

    [Fact]
    public async Task GetByPetIdAsync_FiltersByPetAndInclusivePeriodAndOrdersNewestFirst()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var otherPet = NewPet(Guid.NewGuid());
        db.Pets.AddRange(pet, otherPet);
        var from = DateTime.UtcNow.AddDays(-5);
        var to = DateTime.UtcNow.AddDays(-1);
        var first = NewLog(pet.Id, from, DateTime.UtcNow.AddHours(-3));
        var tieOlder = NewLog(pet.Id, to, DateTime.UtcNow.AddHours(-2));
        var tieNewer = NewLog(pet.Id, to, DateTime.UtcNow.AddHours(-1));
        db.PetWeightLogs.AddRange(
            NewLog(pet.Id, from.AddTicks(-1)),
            first,
            tieOlder,
            tieNewer,
            NewLog(pet.Id, to.AddTicks(1)),
            NewLog(otherPet.Id, to));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PetWeightLogRepository(db);

        var result = await repo.GetByPetIdAsync(pet.Id, from, to);

        Assert.Equal([tieNewer.Id, tieOlder.Id, first.Id], result.Select(x => x.Id));
    }

    [Fact]
    public async Task GetByPetIdAsync_ReturnsDetachedEntities()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var log = NewLog(pet.Id, DateTime.UtcNow.AddDays(-1));
        db.AddRange(pet, log);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var repo = new PetWeightLogRepository(db);

        var result = await repo.GetByPetIdAsync(pet.Id);

        Assert.Single(result);
        Assert.Empty(db.ChangeTracker.Entries<PetWeightLog>());
    }

    [Fact]
    public async Task GetTrackedByIdAsync_ReturnsTrackedEntityOrNull()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var log = NewLog(pet.Id, DateTime.UtcNow.AddDays(-1));
        db.AddRange(pet, log);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var repo = new PetWeightLogRepository(db);

        var found = await repo.GetTrackedByIdAsync(log.Id);
        var missing = await repo.GetTrackedByIdAsync(Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal(EntityState.Unchanged, db.Entry(found).State);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetLatestByPetIdAsync_UsesMeasuredAtThenCreatedAtAndReturnsDetachedEntity()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        var measuredAt = DateTime.UtcNow.AddDays(-1);
        var olderCreated = NewLog(pet.Id, measuredAt, DateTime.UtcNow.AddHours(-2));
        var newerCreated = NewLog(pet.Id, measuredAt, DateTime.UtcNow.AddHours(-1));
        db.PetWeightLogs.AddRange(olderCreated, newerCreated);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var repo = new PetWeightLogRepository(db);

        var result = await repo.GetLatestByPetIdAsync(pet.Id);

        Assert.Equal(newerCreated.Id, result!.Id);
        Assert.Empty(db.ChangeTracker.Entries<PetWeightLog>());
    }

    [Fact]
    public async Task GetTrackedPetByIdAsync_ReturnsTrackedPetOrNull()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var repo = new PetWeightLogRepository(db);

        var found = await repo.GetTrackedPetByIdAsync(pet.Id);
        var missing = await repo.GetTrackedPetByIdAsync(Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal(EntityState.Unchanged, db.Entry(found).State);
        Assert.Null(missing);
    }

    [Fact]
    public async Task ExistsForPetAtMeasuredAtAsync_MatchesPetAndTimestampAndHonorsExcludedId()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var otherPet = NewPet(Guid.NewGuid());
        var measuredAt = DateTime.UtcNow.AddDays(-1);
        var log = NewLog(pet.Id, measuredAt);
        db.AddRange(pet, otherPet, log, NewLog(otherPet.Id, measuredAt));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PetWeightLogRepository(db);

        Assert.True(await repo.ExistsForPetAtMeasuredAtAsync(pet.Id, measuredAt));
        Assert.False(await repo.ExistsForPetAtMeasuredAtAsync(pet.Id, measuredAt, log.Id));
        Assert.False(await repo.ExistsForPetAtMeasuredAtAsync(pet.Id, measuredAt.AddTicks(1)));
    }

    [Fact]
    public async Task AddDeleteAndSaveChangesAsync_PersistExpectedState()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new PetWeightLogRepository(db);
        var log = NewLog(pet.Id, DateTime.UtcNow.AddDays(-1));

        var added = await repo.AddAsync(log);
        var addedCount = await repo.SaveChangesAsync();
        repo.Delete(log);
        var deletedCount = await repo.SaveChangesAsync();

        Assert.Same(log, added);
        Assert.Equal(1, addedCount);
        Assert.Equal(1, deletedCount);
        Assert.False(await db.PetWeightLogs.AnyAsync(x => x.Id == log.Id, TestContext.Current.CancellationToken));
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
        Id = Guid.NewGuid(), UserId = userId, Name = "Pet", Species = Enums.AnimalSpecies.Dog
    };

    private static PetWeightLog NewLog(Guid petId, DateTime measuredAt, DateTime? createdAt = null) => new()
    {
        Id = Guid.NewGuid(), PetId = petId, WeightKg = 10m, MeasuredAt = measuredAt,
        CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-2)
    };
}
