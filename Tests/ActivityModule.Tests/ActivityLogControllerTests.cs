using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.ActivityModule.Api;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class ActivityLogControllerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _logId = Guid.NewGuid();

    [Fact]
    public async Task GetAll_ReturnsOkAndForwardsFilters()
    {
        var expected = new List<ActivityLogResponseDto> { new() { Id = _logId } };
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        var forwarded = false;
        var service = new FakeActivityLogService
        {
            GetByPetId = (petId, userId, actualFrom, actualTo, actualSource) =>
            {
                forwarded = petId == _petId && userId == _userId && actualFrom == from &&
                    actualTo == to && actualSource == ActivitySource.Manual;
                return Task.FromResult<IReadOnlyList<ActivityLogResponseDto>>(expected);
            }
        };

        var result = await Controller(service).GetAll(_petId, from, to, ActivitySource.Manual);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.True(forwarded);
    }

    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task GetAll_LetsDomainFailuresReachTheHandler(bool notFound, int expectedStatus)
    {
        var service = new FakeActivityLogService
        {
            GetByPetId = (_, _, _, _, _) => notFound
                ? Task.FromException<IReadOnlyList<ActivityLogResponseDto>>(new NotFoundException(ErrorCodes.PetNotFound, "Pet not found"))
                : Task.FromException<IReadOnlyList<ActivityLogResponseDto>>(new ValidationException(ErrorCodes.Activity.DateRangeInvalid, "Invalid range"))
        };

        var exception = await Assert.ThrowsAnyAsync<AppException>(() =>
            Controller(service).GetAll(_petId, null, null, null));

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.NotNull(exception.Code);
    }

    [Fact]
    public async Task GetById_ReturnsOkOrNotFound()
    {
        var dto = new ActivityLogResponseDto { Id = _logId };
        var service = new FakeActivityLogService { GetById = (_, _, _) => Task.FromResult<ActivityLogResponseDto?>(dto) };

        var ok = Assert.IsType<OkObjectResult>(await Controller(service).GetById(_petId, _logId));
        Assert.Same(dto, ok.Value);

        service.GetById = (_, _, _) => Task.FromResult<ActivityLogResponseDto?>(null);
        var missing = Assert.IsType<NotFoundObjectResult>(await Controller(service).GetById(_petId, _logId));
        Assert.IsType<ApiErrorResponse>(missing.Value);
    }

    [Fact]
    public async Task Create_Returns201PointingAtTheNewLog()
    {
        var created = new ActivityLogResponseDto { Id = _logId, PetId = _petId };
        var service = new FakeActivityLogService { Create = (_, _, _) => Task.FromResult(created) };

        var result = await Controller(service).Create(_petId, new CreateActivityLogDto());

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ActivityLogController.GetById), createdResult.ActionName);
        Assert.Equal(_petId, createdResult.RouteValues!["petId"]);
        Assert.Equal(_logId, createdResult.RouteValues!["activityLogId"]);
        Assert.Same(created, createdResult.Value);
    }

    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task Create_LetsDomainFailuresReachTheHandler(bool notFound, int expectedStatus)
    {
        var service = new FakeActivityLogService
        {
            Create = (_, _, _) => notFound
                ? Task.FromException<ActivityLogResponseDto>(new NotFoundException(ErrorCodes.PetNotFound, "Pet not found"))
                : Task.FromException<ActivityLogResponseDto>(new ValidationException(ErrorCodes.Activity.StepsNegative, "Steps cannot be negative"))
        };

        var exception = await Assert.ThrowsAnyAsync<AppException>(() =>
            Controller(service).Create(_petId, new CreateActivityLogDto()));

        Assert.Equal(expectedStatus, exception.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns204ThenNotFound()
    {
        var service = new FakeActivityLogService { Delete = (_, _, _) => Task.FromResult(true) };
        Assert.IsType<NoContentResult>(await Controller(service).Delete(_petId, _logId));

        service.Delete = (_, _, _) => Task.FromResult(false);
        var missing = Assert.IsType<NotFoundObjectResult>(await Controller(service).Delete(_petId, _logId));
        Assert.IsType<ApiErrorResponse>(missing.Value);
    }

    [Fact]
    public async Task Update_ReturnsOkAndForwardsTheIds()
    {
        var updated = new ActivityLogResponseDto { Id = _logId };
        var dto = new PatchActivityLogDto();
        var forwarded = false;
        var service = new FakeActivityLogService
        {
            Update = (petId, logId, userId, actualDto) =>
            {
                forwarded = petId == _petId && logId == _logId && userId == _userId && ReferenceEquals(actualDto, dto);
                return Task.FromResult(updated);
            }
        };

        var result = await Controller(service).Update(_petId, _logId, dto);

        Assert.Same(updated, Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(forwarded);
    }

    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task Update_LetsDomainFailuresReachTheHandler(bool notFound, int expectedStatus)
    {
        var service = new FakeActivityLogService
        {
            Update = (_, _, _, _) => notFound
                ? Task.FromException<ActivityLogResponseDto>(new NotFoundException(ErrorCodes.Activity.LogNotFound, "Activity log not found"))
                : Task.FromException<ActivityLogResponseDto>(new ValidationException(ErrorCodes.Activity.StepsNegative, "Steps cannot be negative"))
        };

        var exception = await Assert.ThrowsAnyAsync<AppException>(() =>
            Controller(service).Update(_petId, _logId, new PatchActivityLogDto()));

        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.NotNull(exception.Code);
    }

    private ActivityLogController Controller(IActivityLogService service)
    {
        var identity = new ClaimsIdentity([new Claim("userId", _userId.ToString())], "test");
        return new ActivityLogController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }
}
