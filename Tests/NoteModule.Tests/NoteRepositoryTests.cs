using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NoteModule.Repository;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NoteModule.Tests;

public class NoteRepositoryTests
{
    [Fact]
    public async Task PetBelongsToUserAsync_RequiresMatchingPetAndUser()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        var pet = NewPet(userId);
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new NoteRepository(db);

        Assert.True(await repo.PetBelongsToUserAsync(pet.Id, userId));
        Assert.False(await repo.PetBelongsToUserAsync(pet.Id, Guid.NewGuid()));
        Assert.False(await repo.PetBelongsToUserAsync(Guid.NewGuid(), userId));
    }

    [Fact]
    public async Task GetByPetIdAsync_FiltersByPetAndOrdersByUpdatedAtNewestFirst()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        var otherPet = NewPet(Guid.NewGuid());
        db.Pets.AddRange(pet, otherPet);
        var oldest = NewNote(pet.Id, DateTime.UtcNow.AddDays(-3));
        var middle = NewNote(pet.Id, DateTime.UtcNow.AddDays(-2));
        var newest = NewNote(pet.Id, DateTime.UtcNow.AddDays(-1));
        db.Notes.AddRange(middle, newest, oldest, NewNote(otherPet.Id, DateTime.UtcNow));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new NoteRepository(db);

        var result = await repo.GetByPetIdAsync(pet.Id);

        Assert.Equal([newest.Id, middle.Id, oldest.Id], result.Select(x => x.Id));
    }

    /// <summary>
    /// The list reads <c>UpdatedAt</c> alone, which is only safe because insert
    /// sets it too — a null would sort unpredictably and the client would have to
    /// fall back to <c>CreatedAt</c>.
    /// </summary>
    [Fact]
    public async Task GetByPetIdAsync_OrdersAnUneditedNoteByItsInsertStamp()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        var edited = NewNote(pet.Id, DateTime.UtcNow.AddDays(-1));
        edited.CreatedAt = DateTime.UtcNow.AddDays(-10);
        var untouched = NewNote(pet.Id, DateTime.UtcNow.AddDays(-5));
        untouched.CreatedAt = untouched.UpdatedAt;
        db.Notes.AddRange(edited, untouched);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new NoteRepository(db);

        var result = await repo.GetByPetIdAsync(pet.Id);

        Assert.Equal([edited.Id, untouched.Id], result.Select(x => x.Id));
    }

    [Fact]
    public async Task GetByPetIdAsync_ReturnsNotesMissingOneField()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        var titleOnly = NewNote(pet.Id, DateTime.UtcNow.AddHours(-1));
        titleOnly.Content = null;
        var contentOnly = NewNote(pet.Id, DateTime.UtcNow.AddHours(-2));
        contentOnly.Title = null;
        db.Notes.AddRange(titleOnly, contentOnly);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new NoteRepository(db);

        var result = await repo.GetByPetIdAsync(pet.Id);

        Assert.Null(result[0].Content);
        Assert.Null(result[1].Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReadsUntrackedAndGetTrackedByIdAsyncReadsTracked()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        var note = NewNote(pet.Id, DateTime.UtcNow);
        db.Notes.Add(note);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var repo = new NoteRepository(db);

        var untracked = await repo.GetByIdAsync(note.Id);
        Assert.NotNull(untracked);
        Assert.Equal(EntityState.Detached, db.Entry(untracked!).State);

        db.ChangeTracker.Clear();
        var tracked = await repo.GetTrackedByIdAsync(note.Id);
        Assert.NotNull(tracked);
        Assert.Equal(EntityState.Unchanged, db.Entry(tracked!).State);

        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
        Assert.Null(await repo.GetTrackedByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AddAsync_AndDelete_PersistThroughSaveChanges()
    {
        await using var db = CreateContext();
        var pet = NewPet(Guid.NewGuid());
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new NoteRepository(db);
        var note = NewNote(pet.Id, DateTime.UtcNow);

        var added = await repo.AddAsync(note);
        var addedCount = await repo.SaveChangesAsync();
        repo.Delete(note);
        var deletedCount = await repo.SaveChangesAsync();

        Assert.Same(note, added);
        Assert.Equal(1, addedCount);
        Assert.Equal(1, deletedCount);
        Assert.False(await db.Notes.AnyAsync(x => x.Id == note.Id, TestContext.Current.CancellationToken));
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

    private static Note NewNote(Guid petId, DateTime updatedAt) => new()
    {
        Id = Guid.NewGuid(),
        PetId = petId,
        Title = "Title",
        Content = "Body",
        CreatedAt = updatedAt,
        UpdatedAt = updatedAt
    };
}
