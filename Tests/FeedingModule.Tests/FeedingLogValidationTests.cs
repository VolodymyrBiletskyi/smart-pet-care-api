using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.FeedingModule.Api;
using smart_pet_care_api.Modules.FeedingModule.Domain;
using smart_pet_care_api.Modules.FeedingModule.DTOs.Requests;
using smart_pet_care_api.Modules.FeedingModule.Repository;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.FeedingModule.Tests;

public class FeedingLogValidationTests
{
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task Create_WhenFedAtIsMissing_ReturnsBadRequest()
    {
        var dto = DeserializeRequest("""
            {
              "foodType": 0
            }
            """);

        var result = await Controller().Create(_petId, dto);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("FedAt is required.", error.Message);
    }

    [Fact]
    public async Task Create_WhenFedAtIsDateTimeMinValue_ReturnsBadRequest()
    {
        var dto = DeserializeRequest("""
            {
              "fedAt": "0001-01-01T00:00:00Z",
              "foodType": 0
            }
            """);

        var result = await Controller().Create(_petId, dto);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("FedAt is required.", error.Message);
    }

    [Fact]
    public async Task Update_WhenPatchIsEmpty_ReturnsBadRequest()
    {
        var result = await Controller().Update(_petId, Guid.NewGuid(), new PatchFeedingLogDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("At least one field must be provided.", error.Message);
    }

    private FeedingLogController Controller()
    {
        var identity = new ClaimsIdentity([new Claim("userId", _userId.ToString())], "test");
        return new FeedingLogController(new FeedingLogService(new FakeFeedingLogRepository()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static CreateFeedingLogDto DeserializeRequest(string json) =>
        JsonSerializer.Deserialize<CreateFeedingLogDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
}

internal sealed class FakeFeedingLogRepository : IFeedingLogRepository
{
    public Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId) => Task.FromResult(true);
    public Task<IReadOnlyList<FeedingLog>> GetByPetIdAsync(Guid petId) =>
        Task.FromResult<IReadOnlyList<FeedingLog>>([]);
    public Task<IReadOnlyList<FeedingLog>> GetByPetIdAndRangeAsync(
        Guid petId, DateTime startUtc, DateTime endUtc) =>
        Task.FromResult<IReadOnlyList<FeedingLog>>([]);
    public Task<FeedingLog?> GetByIdAsync(Guid id) => Task.FromResult<FeedingLog?>(null);
    public Task<FeedingLog?> GetTrackedByIdAsync(Guid id) => Task.FromResult<FeedingLog?>(null);
    public Task<FeedingLog> AddAsync(FeedingLog entity) => Task.FromResult(entity);
    public void Delete(FeedingLog entity) { }
    public Task<int> SaveChangesAsync() => Task.FromResult(1);
}
