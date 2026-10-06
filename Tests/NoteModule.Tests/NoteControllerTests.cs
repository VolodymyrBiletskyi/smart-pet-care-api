using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.NoteModule.Api;
using smart_pet_care_api.Modules.NoteModule.Domain;
using smart_pet_care_api.Modules.NoteModule.DTOs.Requests;
using smart_pet_care_api.Modules.NoteModule.DTOs.Responses;
using Xunit;

namespace smart_pet_care_api.Modules.NoteModule.Tests;

public class NoteControllerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _noteId = Guid.NewGuid();

    [Fact]
    public async Task GetAll_ReturnsOkAndForwardsTheCallerIdentity()
    {
        var expected = new List<NoteResponseDto> { new() { Id = _noteId } };
        var forwarded = false;
        var service = new FakeNoteService
        {
            GetByPetId = (petId, userId) =>
            {
                forwarded = petId == _petId && userId == _userId;
                return Task.FromResult<IReadOnlyList<NoteResponseDto>>(expected);
            }
        };

        var result = await Controller(service).GetAll(_petId);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(forwarded);
    }

    [Fact]
    public async Task GetById_ReturnsOkOrNotFound()
    {
        var dto = new NoteResponseDto { Id = _noteId };
        var service = new FakeNoteService { GetById = (_, _, _) => Task.FromResult<NoteResponseDto?>(dto) };

        var ok = Assert.IsType<OkObjectResult>(await Controller(service).GetById(_petId, _noteId));
        Assert.Same(dto, ok.Value);

        service.GetById = (_, _, _) => Task.FromResult<NoteResponseDto?>(null);
        var missing = Assert.IsType<NotFoundObjectResult>(await Controller(service).GetById(_petId, _noteId));
        Assert.Equal(ErrorCodes.Note.NotFound, Assert.IsType<ApiErrorResponse>(missing.Value).Code);
    }

    [Fact]
    public async Task Create_Returns201PointingAtTheNewNote()
    {
        var created = new NoteResponseDto { Id = _noteId, PetId = _petId };
        var service = new FakeNoteService { Create = (_, _, _) => Task.FromResult(created) };

        var result = await Controller(service).Create(_petId, new CreateNoteDto { Title = "Title" });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(NoteController.GetById), createdResult.ActionName);
        Assert.Equal(_petId, createdResult.RouteValues!["petId"]);
        Assert.Equal(_noteId, createdResult.RouteValues!["noteId"]);
        Assert.Same(created, createdResult.Value);
    }

    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task Create_LetsDomainFailuresReachTheHandler(bool notFound, int expectedStatus)
    {
        var service = new FakeNoteService
        {
            Create = (_, _, _) => notFound
                ? Task.FromException<NoteResponseDto>(new NotFoundException(ErrorCodes.PetNotFound, "Pet not found"))
                : Task.FromException<NoteResponseDto>(new ValidationException(ErrorCodes.Note.Empty, "A note must have a title or content"))
        };

        var exception = await Assert.ThrowsAnyAsync<AppException>(() =>
            Controller(service).Create(_petId, new CreateNoteDto()));

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.NotNull(exception.Code);
    }

    [Fact]
    public async Task Update_ReturnsOkAndForwardsTheIdsAndBody()
    {
        var updated = new NoteResponseDto { Id = _noteId };
        var dto = new PatchNoteDto();
        var forwarded = false;
        var service = new FakeNoteService
        {
            Update = (petId, noteId, userId, actualDto) =>
            {
                forwarded = petId == _petId && noteId == _noteId && userId == _userId && ReferenceEquals(actualDto, dto);
                return Task.FromResult(updated);
            }
        };

        var result = await Controller(service).Update(_petId, _noteId, dto);

        Assert.Same(updated, Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(forwarded);
    }

    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task Update_LetsDomainFailuresReachTheHandler(bool notFound, int expectedStatus)
    {
        var service = new FakeNoteService
        {
            Update = (_, _, _, _) => notFound
                ? Task.FromException<NoteResponseDto>(new NotFoundException(ErrorCodes.Note.NotFound, "Note not found"))
                : Task.FromException<NoteResponseDto>(new ValidationException(ErrorCodes.Note.UpdateEmpty, "At least one field must be provided"))
        };

        var exception = await Assert.ThrowsAnyAsync<AppException>(() =>
            Controller(service).Update(_petId, _noteId, new PatchNoteDto()));

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.NotNull(exception.Code);
    }

    [Fact]
    public async Task Delete_Returns204ThenNotFound()
    {
        var service = new FakeNoteService { Delete = (_, _, _) => Task.FromResult(true) };
        Assert.IsType<NoContentResult>(await Controller(service).Delete(_petId, _noteId));

        service.Delete = (_, _, _) => Task.FromResult(false);
        var missing = Assert.IsType<NotFoundObjectResult>(await Controller(service).Delete(_petId, _noteId));
        Assert.Equal(ErrorCodes.Note.NotFound, Assert.IsType<ApiErrorResponse>(missing.Value).Code);
    }

    private NoteController Controller(INoteService service)
    {
        var identity = new ClaimsIdentity([new Claim("userId", _userId.ToString())], "test");
        return new NoteController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }
}
