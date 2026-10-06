using smart_pet_care_api.Common.Patching;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NoteModule.DTOs.Requests;
using smart_pet_care_api.Modules.NoteModule.Mapper;
using Xunit;

namespace smart_pet_care_api.Modules.NoteModule.Tests;

public class NoteMapperTests
{
    [Fact]
    public void ToEntity_TrimsBothFieldsAndStampsThePet()
    {
        var petId = Guid.NewGuid();

        var entity = NoteMapper.ToEntity(new CreateNoteDto { Title = "  Title  ", Content = "  Body  " }, petId);

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(petId, entity.PetId);
        Assert.Equal("Title", entity.Title);
        Assert.Equal("Body", entity.Content);
        Assert.Equal(entity.CreatedAt, entity.UpdatedAt);
        Assert.InRange(entity.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToEntity_StoresABlankFieldAsNull(string? blank)
    {
        var entity = NoteMapper.ToEntity(new CreateNoteDto { Title = blank, Content = "Body" }, Guid.NewGuid());

        Assert.Null(entity.Title);
    }

    [Fact]
    public void ToDto_CopiesEveryField()
    {
        var note = new Note
        {
            PetId = Guid.NewGuid(),
            Title = "Title",
            Content = "Body",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-3)
        };

        var dto = note.ToDto();

        Assert.Equal(note.Id, dto.Id);
        Assert.Equal(note.PetId, dto.PetId);
        Assert.Equal(note.Title, dto.Title);
        Assert.Equal(note.Content, dto.Content);
        Assert.Equal(note.CreatedAt, dto.CreatedAt);
        Assert.Equal(note.UpdatedAt, dto.UpdatedAt);
    }

    [Fact]
    public void PatchEntity_LeavesAnAbsentFieldAloneAndBumpsUpdatedAt()
    {
        var note = NewNote();
        var before = note.UpdatedAt;

        note.PatchEntity(new PatchNoteDto { Title = PatchField<string>.Set("  New title  ") });

        Assert.Equal("New title", note.Title);
        Assert.Equal("Body", note.Content);
        Assert.True(note.UpdatedAt > before);
        Assert.Equal(DateTime.UtcNow.Date, note.UpdatedAt.Date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PatchEntity_ClearsASetFieldToNull(string? cleared)
    {
        var note = NewNote();

        note.PatchEntity(new PatchNoteDto { Content = PatchField<string>.Set(cleared) });

        Assert.Equal("Title", note.Title);
        Assert.Null(note.Content);
    }

    private static Note NewNote() => new()
    {
        PetId = Guid.NewGuid(),
        Title = "Title",
        Content = "Body",
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow.AddDays(-1)
    };
}
