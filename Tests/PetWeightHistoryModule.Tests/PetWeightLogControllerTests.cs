using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
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

    /// <summary>
    /// Mapping a domain failure to a status is GlobalExceptionHandler's job now,
    /// so the controller's contribution is to stay out of the way: it must not
    /// swallow the exception and hand back a status of its own invention.
    /// </summary>
    [Fact]
    public async Task GetAll_LetsDomainFailuresReachTheHandler()
    {
        var service = new FakePetWeightLogService
        {
            GetByPetId = (_, _, _, _) => Task.FromException<IReadOnlyList<PetWeightLogResponseDto>>(
                new NotFoundException(ErrorCodes.PetNotFound, "Pet not found"))
        };

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            Controller(service).GetAll(_petId, null, null));

        Assert.Equal(ErrorCodes.PetNotFound, exception.Code);
        Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
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

    [Fact]
    public async Task Create_LetsTheConflictReachTheHandlerWithItsAlias()
    {
        var service = new FakePetWeightLogService
        {
            Create = (_, _, _) => Task.FromException<PetWeightLogResponseDto>(
                new PetWeightLogConflictException("Duplicate"))
        };

        var exception = await Assert.ThrowsAsync<PetWeightLogConflictException>(() =>
            Controller(service).Create(_petId, new CreatePetWeightLogDto()));

        Assert.Equal(ErrorCodes.WeightLog.MeasurementTimeConflict, exception.Code);
        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
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

    [Fact]
    public async Task Update_LetsValidationFailuresReachTheHandlerWithTheirParams()
    {
        var service = new FakePetWeightLogService
        {
            Update = (_, _, _, _) => Task.FromException<PetWeightLogResponseDto>(
                new ValidationException(
                    ErrorCodes.WeightLog.WeightTooLarge,
                    "WeightKg cannot be greater than 230",
                    new Dictionary<string, object?> { ["max"] = 230m }))
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Controller(service).Update(_petId, _logId, new PatchPetWeightLogDto()));

        Assert.Equal(ErrorCodes.WeightLog.WeightTooLarge, exception.Code);
        Assert.Equal(230m, exception.Parameters?["max"]);
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
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("weight_log_not_found", response.Code);
        Assert.Equal("Weight log not found.", response.Message);
    }

    [Fact]
    public async Task Delete_WhenPetDoesNotExist_LetsTheDomainFailureThrough()
    {
        var service = new FakePetWeightLogService
        {
            Delete = (_, _, _) => Task.FromException<bool>(
                new NotFoundException(ErrorCodes.PetNotFound, "Pet not found"))
        };

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            Controller(service).Delete(_petId, _logId));

        Assert.Equal(ErrorCodes.PetNotFound, exception.Code);
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
        var response = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("authentication_token_invalid", response.Code);
        Assert.Equal("Authentication token is invalid.", response.Message);
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
        var response = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("authentication_token_invalid", response.Code);
        Assert.Equal("Authentication token is invalid.", response.Message);
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
