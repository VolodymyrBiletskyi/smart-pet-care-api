using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.ActivityModule.Api;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using Xunit;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class SleepLogControllerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _logId = Guid.NewGuid();

    [Fact]
    public async Task GetAll_ReturnsOkAndForwardsFilters()
    {
        var expected = new List<SleepLogResponseDto> { new() { Id = _logId } };
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        var forwarded = false;
        var service = new FakeSleepLogService
        {
            GetByPetId = (petId, userId, actualFrom, actualTo) =>
            {
                forwarded = petId == _petId && userId == _userId && actualFrom == from && actualTo == to;
                return Task.FromResult<IReadOnlyList<SleepLogResponseDto>>(expected);
            }
        };

        var result = await Controller(service).GetAll(_petId, from, to);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(forwarded);
    }

    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task GetAll_MapsDomainErrors(bool notFound, int expectedStatus)
    {
        var service = new FakeSleepLogService
        {
            GetByPetId = (_, _, _, _) => notFound
                ? Task.FromException<IReadOnlyList<SleepLogResponseDto>>(new InvalidOperationException("Pet not found"))
                : Task.FromException<IReadOnlyList<SleepLogResponseDto>>(new ArgumentException("Invalid range"))
        };

        var result = await Controller(service).GetAll(_petId, null, null);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.IsType<ApiErrorResponse>(objectResult.Value);
    }

    [Fact]
    public async Task GetById_ReturnsOkOrNotFound()
    {
        var dto = new SleepLogResponseDto { Id = _logId };
        var service = new FakeSleepLogService { GetById = (_, _, _) => Task.FromResult<SleepLogResponseDto?>(dto) };

        Assert.Same(dto, Assert.IsType<OkObjectResult>(await Controller(service).GetById(_petId, _logId)).Value);

        service.GetById = (_, _, _) => Task.FromResult<SleepLogResponseDto?>(null);
        var missing = Assert.IsType<NotFoundObjectResult>(await Controller(service).GetById(_petId, _logId));
        Assert.IsType<ApiErrorResponse>(missing.Value);
    }

    [Fact]
    public async Task Create_Returns201PointingAtTheNewLog()
    {
        var created = new SleepLogResponseDto { Id = _logId, PetId = _petId };
        var service = new FakeSleepLogService { Create = (_, _, _) => Task.FromResult(created) };

        var result = await Controller(service).Create(_petId, new CreateSleepLogDto());

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(SleepLogController.GetById), createdResult.ActionName);
        Assert.Equal(_petId, createdResult.RouteValues!["petId"]);
        Assert.Equal(_logId, createdResult.RouteValues!["sleepLogId"]);
        Assert.Same(created, createdResult.Value);
    }

    /// <summary>
    /// The 24-hour cap surfaces as a 400, not a 409: the day is not a resource the caller can
    /// discover a conflict with, it is a value the request got wrong.
    /// </summary>
    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task Create_MapsDomainErrors(bool notFound, int expectedStatus)
    {
        var service = new FakeSleepLogService
        {
            Create = (_, _, _) => notFound
                ? Task.FromException<SleepLogResponseDto>(new InvalidOperationException("Pet not found"))
                : Task.FromException<SleepLogResponseDto>(new ArgumentException("would total more than 24 hours"))
        };

        var result = await Controller(service).Create(_petId, new CreateSleepLogDto());

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Delete_Returns204ThenNotFound()
    {
        var service = new FakeSleepLogService { Delete = (_, _, _) => Task.FromResult(true) };
        Assert.IsType<NoContentResult>(await Controller(service).Delete(_petId, _logId));

        service.Delete = (_, _, _) => Task.FromResult(false);
        var missing = Assert.IsType<NotFoundObjectResult>(await Controller(service).Delete(_petId, _logId));
        Assert.IsType<ApiErrorResponse>(missing.Value);
    }

    [Fact]
    public async Task Update_ReturnsOkAndForwardsTheIds()
    {
        var updated = new SleepLogResponseDto { Id = _logId };
        var dto = new PatchSleepLogDto();
        var forwarded = false;
        var service = new FakeSleepLogService
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

    /// <summary>
    /// An edit that busts the day's 24 hours is a 400 for the same reason a create is: the day
    /// is a value the request got wrong, not a resource to conflict with.
    /// </summary>
    [Theory]
    [InlineData(true, 404)]
    [InlineData(false, 400)]
    public async Task Update_MapsDomainErrors(bool notFound, int expectedStatus)
    {
        var service = new FakeSleepLogService
        {
            Update = (_, _, _, _) => notFound
                ? Task.FromException<SleepLogResponseDto>(new InvalidOperationException("Sleep log not found"))
                : Task.FromException<SleepLogResponseDto>(new ArgumentException("would total more than 24 hours"))
        };

        var result = await Controller(service).Update(_petId, _logId, new PatchSleepLogDto());

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.IsType<ApiErrorResponse>(objectResult.Value);
    }

    private SleepLogController Controller(ISleepLogService service)
    {
        var identity = new ClaimsIdentity([new Claim("userId", _userId.ToString())], "test");
        return new SleepLogController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }
}
