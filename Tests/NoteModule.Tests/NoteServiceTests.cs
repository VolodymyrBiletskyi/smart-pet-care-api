using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Common.Patching;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NoteModule.Domain;
using smart_pet_care_api.Modules.NoteModule.DTOs.Requests;
using smart_pet_care_api.Modules.NoteModule.Repository;
using Xunit;

namespace smart_pet_care_api.Modules.NoteModule.Tests;

public class NoteServiceTests
{
    private const int TitleMaxLength = 200;
    private const int ContentMaxLength = 10000;

    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _noteId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_PersistsTrimmedFieldsAndReturnsDto()
    {
        var repo = new FakeNoteRepository();

        var result = await Service(repo).CreateAsync(_petId, _userId, new CreateNoteDto
        {
            Title = "  Sitter's phone  ",
            Content = "  +380 00 000 00 00  "
        });

        Assert.NotNull(repo.AddedNote);
        var added = repo.AddedNote!;
        Assert.Equal(_petId, added.PetId);
        Assert.Equal("Sitter's phone", added.Title);
        Assert.Equal("+380 00 000 00 00", added.Content);
        Assert.Equal(added.CreatedAt, added.UpdatedAt);
        Assert.Equal(1, repo.SaveChangesCalls);

        Assert.Equal(added.Id, result.Id);
        Assert.Equal("Sitter's phone", result.Title);
        Assert.Equal("+380 00 000 00 00", result.Content);
    }

    /// <summary>
    /// A heading with nothing under it and a body with no heading are both notes
    /// the owner meant to keep, so either field alone is enough.
    /// </summary>
    [Theory]
    [InlineData("Buy the large bag", null)]
    [InlineData("Buy the large bag", "")]
    [InlineData("Buy the large bag", "   ")]
    [InlineData(null, "Vet said to come back in a month")]
    [InlineData("", "Vet said to come back in a month")]
    [InlineData("   ", "Vet said to come back in a month")]
    public async Task CreateAsync_AcceptsEitherFieldOnTheirOwn(string? title, string? content)
    {
        var repo = new FakeNoteRepository();

        await Service(repo).CreateAsync(_petId, _userId, new CreateNoteDto { Title = title, Content = content });

        Assert.NotNull(repo.AddedNote);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    /// <summary>
    /// A blank field is stored as null rather than "", so every reader has one
    /// shape to check for "absent".
    /// </summary>
    [Fact]
    public async Task CreateAsync_StoresABlankFieldAsNull()
    {
        var repo = new FakeNoteRepository();

        var result = await Service(repo).CreateAsync(_petId, _userId, new CreateNoteDto
        {
            Title = "   ",
            Content = "Just the body"
        });

        Assert.Null(repo.AddedNote!.Title);
        Assert.Null(result.Title);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "\t \n")]
    public async Task CreateAsync_RejectsANoteWithNeitherField(string? title, string? content)
    {
        var repo = new FakeNoteRepository();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repo).CreateAsync(_petId, _userId, new CreateNoteDto { Title = title, Content = content }));

        Assert.Equal(ErrorCodes.Note.Empty, ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Null(repo.AddedNote);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_RejectsATooLongTitle()
    {
        var repo = new FakeNoteRepository();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repo).CreateAsync(_petId, _userId, new CreateNoteDto
            {
                Title = new string('t', TitleMaxLength + 1),
                Content = "Body"
            }));

        Assert.Equal(ErrorCodes.Note.TitleTooLong, ex.Code);
        Assert.Equal(TitleMaxLength, ex.Parameters!["maxLength"]);
        Assert.Null(repo.AddedNote);
    }

    [Fact]
    public async Task CreateAsync_RejectsTooLongContent()
    {
        var repo = new FakeNoteRepository();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repo).CreateAsync(_petId, _userId, new CreateNoteDto
            {
                Title = "Title",
                Content = new string('c', ContentMaxLength + 1)
            }));

        Assert.Equal(ErrorCodes.Note.ContentTooLong, ex.Code);
        Assert.Equal(ContentMaxLength, ex.Parameters!["maxLength"]);
        Assert.Null(repo.AddedNote);
    }

    /// <summary>
    /// The ceiling is on what the field holds after trimming, not on what arrived.
    /// </summary>
    [Fact]
    public async Task CreateAsync_MeasuresLengthAfterTrimming()
    {
        var repo = new FakeNoteRepository();

        await Service(repo).CreateAsync(_petId, _userId, new CreateNoteDto
        {
            Title = $"   {new string('t', TitleMaxLength)}   ",
            Content = "Body"
        });

        Assert.Equal(TitleMaxLength, repo.AddedNote!.Title!.Length);
    }

    [Fact]
    public async Task CreateAsync_RejectsAPetTheUserDoesNotOwn()
    {
        var repo = new FakeNoteRepository { PetBelongsToUser = false };

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(repo).CreateAsync(_petId, _userId, new CreateNoteDto { Title = "Title", Content = "Body" }));

        Assert.Equal(ErrorCodes.PetNotFound, ex.Code);
        Assert.Null(repo.AddedNote);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_AppliesOnlyTheFieldsThatWereSent()
    {
        var note = NewNote("Old title", "Old body");
        var repo = new FakeNoteRepository { TrackedNote = note };
        var before = note.UpdatedAt;

        var result = await Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
        {
            Title = PatchField<string>.Set("  New title  ")
        });

        Assert.Equal("New title", note.Title);
        Assert.Equal("Old body", note.Content);
        Assert.True(note.UpdatedAt > before);
        Assert.Equal("New title", result.Title);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    /// <summary>
    /// `null` clears, as everywhere else in the repo — the old contract rejected it.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_ClearsAFieldWhileTheOtherSurvives(string? cleared)
    {
        var note = NewNote("Old title", "Old body");
        var repo = new FakeNoteRepository { TrackedNote = note };

        var result = await Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
        {
            Title = PatchField<string>.Set(cleared)
        });

        Assert.Null(note.Title);
        Assert.Equal("Old body", note.Content);
        Assert.Null(result.Title);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    /// <summary>
    /// The rule is about the pair, so clearing one field can only be judged
    /// against what the other holds after the patch.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_RejectsClearingTheLastRemainingField()
    {
        var note = NewNote(title: null, content: "The only thing this note says");
        var repo = new FakeNoteRepository { TrackedNote = note };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
            {
                Content = PatchField<string>.Set(null)
            }));

        Assert.Equal(ErrorCodes.Note.Empty, ex.Code);
        Assert.Equal("The only thing this note says", note.Content);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_RejectsClearingBothFieldsAtOnce()
    {
        var note = NewNote("Old title", "Old body");
        var repo = new FakeNoteRepository { TrackedNote = note };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
            {
                Title = PatchField<string>.Set(null),
                Content = PatchField<string>.Set("  ")
            }));

        Assert.Equal(ErrorCodes.Note.Empty, ex.Code);
        Assert.Equal("Old title", note.Title);
        Assert.Equal("Old body", note.Content);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    /// <summary>
    /// Trading one field for the other in a single patch has to pass: neither
    /// half is valid on its own, only the row that results.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_AllowsSwappingWhichFieldCarriesTheNote()
    {
        var note = NewNote("Old title", content: null);
        var repo = new FakeNoteRepository { TrackedNote = note };

        await Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
        {
            Title = PatchField<string>.Set(null),
            Content = PatchField<string>.Set("Now it is the body that says it")
        });

        Assert.Null(note.Title);
        Assert.Equal("Now it is the body that says it", note.Content);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_FillsInAFieldThatWasEmpty()
    {
        var note = NewNote(title: null, content: "Body");
        var repo = new FakeNoteRepository { TrackedNote = note };

        await Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
        {
            Title = PatchField<string>.Set("A heading at last")
        });

        Assert.Equal("A heading at last", note.Title);
        Assert.Equal("Body", note.Content);
    }

    [Fact]
    public async Task UpdateAsync_RevalidatesLengthOnThePatchedRow()
    {
        var note = NewNote("Old title", "Old body");
        var repo = new FakeNoteRepository { TrackedNote = note };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
            {
                Content = PatchField<string>.Set(new string('c', ContentMaxLength + 1))
            }));

        Assert.Equal(ErrorCodes.Note.ContentTooLong, ex.Code);
        Assert.Equal("Old body", note.Content);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_RejectsAnEmptyBody()
    {
        var repo = new FakeNoteRepository { TrackedNote = NewNote("Old title", "Old body") };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repo).UpdateAsync(_petId, _noteId, _userId, new PatchNoteDto()));

        Assert.Equal(ErrorCodes.Note.UpdateEmpty, ex.Code);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_RejectsAMissingNote()
    {
        var repo = new FakeNoteRepository { TrackedNote = null };

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(repo).UpdateAsync(_petId, _noteId, _userId, new PatchNoteDto
            {
                Title = PatchField<string>.Set("New title")
            }));

        Assert.Equal(ErrorCodes.Note.NotFound, ex.Code);
    }

    /// <summary>
    /// Owning the pet in the route is not owning the note: a note belonging to
    /// another pet is not reachable through this one.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_RejectsANoteFiledUnderAnotherPet()
    {
        var note = NewNote("Old title", "Old body");
        note.PetId = Guid.NewGuid();
        var repo = new FakeNoteRepository { TrackedNote = note };

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
            {
                Title = PatchField<string>.Set("New title")
            }));

        Assert.Equal(ErrorCodes.Note.NotFound, ex.Code);
        Assert.Equal("Old title", note.Title);
    }

    [Fact]
    public async Task UpdateAsync_RejectsAPetTheUserDoesNotOwn()
    {
        var note = NewNote("Old title", "Old body");
        var repo = new FakeNoteRepository { PetBelongsToUser = false, TrackedNote = note };

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(repo).UpdateAsync(_petId, note.Id, _userId, new PatchNoteDto
            {
                Title = PatchField<string>.Set("New title")
            }));

        Assert.Equal(ErrorCodes.PetNotFound, ex.Code);
        Assert.Equal("Old title", note.Title);
    }

    [Fact]
    public async Task GetByPetIdAsync_MapsEveryNoteInOrder()
    {
        var first = NewNote("First", "Body");
        var second = NewNote(title: null, content: "Body only");
        var repo = new FakeNoteRepository { Notes = [first, second] };

        var result = await Service(repo).GetByPetIdAsync(_petId, _userId);

        Assert.Equal([first.Id, second.Id], result.Select(x => x.Id));
        Assert.Null(result[1].Title);
        Assert.Equal("Body only", result[1].Content);
    }

    [Fact]
    public async Task GetByPetIdAsync_RejectsAPetTheUserDoesNotOwn()
    {
        var repo = new FakeNoteRepository { PetBelongsToUser = false };

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => Service(repo).GetByPetIdAsync(_petId, _userId));

        Assert.Equal(ErrorCodes.PetNotFound, ex.Code);
    }

    [Fact]
    public async Task GetByIdAsync_AnswersNullForAMissingOrForeignNote()
    {
        var note = NewNote("Title", "Body");
        var repo = new FakeNoteRepository { Note = note };

        Assert.Equal(note.Id, (await Service(repo).GetByIdAsync(_petId, note.Id, _userId))!.Id);

        note.PetId = Guid.NewGuid();
        Assert.Null(await Service(repo).GetByIdAsync(_petId, note.Id, _userId));

        repo.Note = null;
        Assert.Null(await Service(repo).GetByIdAsync(_petId, note.Id, _userId));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheNoteThenAnswersFalse()
    {
        var note = NewNote("Title", "Body");
        var repo = new FakeNoteRepository { TrackedNote = note };

        Assert.True(await Service(repo).DeleteAsync(_petId, note.Id, _userId));
        Assert.Same(note, repo.DeletedNote);
        Assert.Equal(1, repo.SaveChangesCalls);

        repo.TrackedNote = null;
        Assert.False(await Service(repo).DeleteAsync(_petId, note.Id, _userId));
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_LeavesANoteFiledUnderAnotherPetAlone()
    {
        var note = NewNote("Title", "Body");
        note.PetId = Guid.NewGuid();
        var repo = new FakeNoteRepository { TrackedNote = note };

        Assert.False(await Service(repo).DeleteAsync(_petId, note.Id, _userId));
        Assert.Null(repo.DeletedNote);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    private static NoteService Service(INoteRepository repo) => new(repo);

    private Note NewNote(string? title, string? content) => new()
    {
        Id = Guid.NewGuid(),
        PetId = _petId,
        Title = title,
        Content = content,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow.AddDays(-1)
    };
}
