using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Api;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Domain;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Responses;
using Xunit;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Tests;

public class PetWeightLogControllerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _logId = Guid.NewGuid();

    [Fact]
    public async Task GetAll_ReturnsOkAndForwardsArguments()
    {
        var expected = new List<PetWeightLogResponseDto> { new() { Id = _logId } };
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        var called = false;
        var service = new FakePetWeightLogService
        {
            GetByPetId = (petId, userId, actualFrom, actualTo) =>
            {
                called = petId == _petId && userId == _userId && actualFrom == from && actualTo == to;
                return Task.FromResult<IReadOnlyList<PetWeightLogResponseDto>>(expected);
            }
        };

        var result = await Controller(service).GetAll(_petId, from, to);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.True(called);
    }

    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task GetAll_MapsDomainErrors(bool notFound, int expectedStatus)
    {
        var service = new FakePetWeightLogService
        {
            GetByPetId = (_, _, _, _) => notFound
                ? Task.FromException<IReadOnlyList<PetWeightLogResponseDto>>(new InvalidOperationException("Pet not found"))
                : Task.FromException<IReadOnlyList<PetWeightLogResponseDto>>(new ArgumentException("Invalid range"))
        };

        var result = await Controller(service).GetAll(_petId, null, null);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithResourceLocation()
    {
        var dto = new CreatePetWeightLogDto { WeightKg = 10m, MeasuredAt = DateTime.UtcNow };
        var expected = new PetWeightLogResponseDto { Id = _logId, PetId = _petId };
        var service = new FakePetWeightLogService { Create = (_, _, _) => Task.FromResult(expected) };

        var result = await Controller(service).Create(_petId, dto);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal($"/api/pets/{_petId}/weight-history", created.Location);
        Assert.Same(expected, created.Value);
    }

    [Theory]
    [InlineData("notFound", 404)]
    [InlineData("badRequest", 400)]
    [InlineData("conflict", 409)]
    public async Task Create_MapsDomainErrors(string error, int expectedStatus)
    {
        var service = new FakePetWeightLogService
        {
            Create = (_, _, _) => error switch
            {
                "notFound" => Task.FromException<PetWeightLogResponseDto>(new InvalidOperationException("Pet not found")),
                "badRequest" => Task.FromException<PetWeightLogResponseDto>(new ArgumentException("Invalid weight")),
                _ => Task.FromException<PetWeightLogResponseDto>(new PetWeightLogConflictException("Duplicate"))
            }
        };

        var result = await Controller(service).Create(_petId, new CreatePetWeightLogDto());

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAndUpdate_WhenDatabaseReportsExpectedUniqueConstraint_ReturnConflict(bool create)
    {
        var service = new FakePetWeightLogService
        {
            Create = (_, _, _) => Task.FromException<PetWeightLogResponseDto>(DuplicateDbUpdateException()),
            Update = (_, _, _, _) => Task.FromException<PetWeightLogResponseDto>(DuplicateDbUpdateException())
        };
        var controller = Controller(service);

        var result = create
            ? await controller.Create(_petId, new CreatePetWeightLogDto())
            : await controller.Update(_petId, _logId, new PatchPetWeightLogDto());

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("A weight log for this pet already exists at the same measurement time.", conflict.Value);
    }

    [Fact]
    public async Task Create_WhenDatabaseErrorIsNotExpectedUniqueConstraint_Rethrows()
    {
        var service = new FakePetWeightLogService
        {
            Create = (_, _, _) => Task.FromException<PetWeightLogResponseDto>(DuplicateDbUpdateException("OtherConstraint"))
        };

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            Controller(service).Create(_petId, new CreatePetWeightLogDto()));
    }
    [Fact]
    public async Task Update_ReturnsOk()
    {
        var expected = new PetWeightLogResponseDto { Id = _logId };
        var service = new FakePetWeightLogService { Update = (_, _, _, _) => Task.FromResult(expected) };

        var result = await Controller(service).Update(_petId, _logId, new PatchPetWeightLogDto());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    [Theory]
    [InlineData("notFound", 404)]
    [InlineData("badRequest", 400)]
    [InlineData("conflict", 409)]
    public async Task Update_MapsDomainErrors(string error, int expectedStatus)
    {
        var service = new FakePetWeightLogService
        {
            Update = (_, _, _, _) => error switch
            {
                "notFound" => Task.FromException<PetWeightLogResponseDto>(new InvalidOperationException("Log not found")),
                "badRequest" => Task.FromException<PetWeightLogResponseDto>(new ArgumentException("Invalid weight")),
                _ => Task.FromException<PetWeightLogResponseDto>(new PetWeightLogConflictException("Duplicate"))
            }
        };

        var result = await Controller(service).Update(_petId, _logId, new PatchPetWeightLogDto());

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenDeleted_ReturnsNoContent()
    {
        var result = await Controller(new FakePetWeightLogService()).Delete(_petId, _logId);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenLogDoesNotExist_ReturnsNotFound()
    {
        var service = new FakePetWeightLogService { Delete = (_, _, _) => Task.FromResult(false) };
        var result = await Controller(service).Delete(_petId, _logId);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenPetDoesNotExist_ReturnsNotFoundMessage()
    {
        var service = new FakePetWeightLogService
        {
            Delete = (_, _, _) => Task.FromException<bool>(new InvalidOperationException("Pet not found"))
        };

        var result = await Controller(service).Delete(_petId, _logId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Pet not found", notFound.Value);
    }

    [Fact]
    public async Task GetAll_WhenUserIdClaimIsMissing_ReturnsUnauthorizedMessage()
    {
        var controller = new PetWeightLogController(new FakePetWeightLogService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetAll(_petId, null, null);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid authentication token", unauthorized.Value);
    }

    [Fact]
    public async Task GetAll_WhenUserIdClaimIsInvalid_ReturnsUnauthorizedMessage()
    {
        var identity = new ClaimsIdentity([new Claim("userId", "not-a-guid")], "test");
        var controller = new PetWeightLogController(new FakePetWeightLogService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        var result = await controller.GetAll(_petId, null, null);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid authentication token", unauthorized.Value);
    }

    private static DbUpdateException DuplicateDbUpdateException(string constraintName = "IX_PetWeightLogs_PetId_MeasuredAt")
    {
        var postgresException = new PostgresException(
            "duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
            constraintName: constraintName);
        return new DbUpdateException("Database update failed", postgresException);
    }
    private PetWeightLogController Controller(IPetWeightLogService service)
    {
        var identity = new ClaimsIdentity([new Claim("userId", _userId.ToString())], "test");
        return new PetWeightLogController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }
}
