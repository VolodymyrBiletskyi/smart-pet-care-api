using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Modules.PetModule.Api;
using smart_pet_care_api.Modules.PetModule.Domain;
using smart_pet_care_api.Modules.PetModule.DTOs;
using Xunit;

namespace smart_pet_care_api.Modules.PetModule.Tests;

public class PetControllerTests
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid petId = Guid.NewGuid();

    [Fact]
    public async Task GetAll_ReturnsOwnedPetsAndForwardsUserId()
    {
        var expected = new List<PetResponseDto> { new() { Id = petId, Name = "Buddy" } };
        var service = new StubPetService { Pets = expected };

        var result = await Controller(service).GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.Equal(userId, service.RequestedUserId);
    }

    [Fact]
    public async Task GetById_WhenPetIsNotOwned_Returns404Contract()
    {
        var result = await Controller(new StubPetService()).GetById(petId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Pet not found.", error.Message);
    }

    [Fact]
    public async Task Create_WhenDomainValidationFails_Returns400Contract()
    {
        var service = new StubPetService
        {
            CreateException = new ArgumentException("Species is invalid")
        };

        var result = await Controller(service).Create(new CreatePetDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("Species is invalid.", error.Message);
    }

    [Fact]
    public async Task Update_WhenPetIsNotOwned_Returns404Contract()
    {
        var service = new StubPetService
        {
            UpdateException = new InvalidOperationException("Pet does not exist")
        };

        var result = await Controller(service).Update(petId, new UpdatePetDto());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Pet does not exist.", error.Message);
    }

    [Fact]
    public async Task Delete_WhenPetIsNotOwned_Returns404Contract()
    {
        var result = await Controller(new StubPetService()).Delete(petId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.IsType<ApiErrorResponse>(notFound.Value);
    }

    private PetController Controller(IPetService service)
    {
        var identity = new ClaimsIdentity([new Claim("userId", userId.ToString())], "test");
        return new PetController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }
}

internal sealed class StubPetService : IPetService
{
    public IReadOnlyList<PetResponseDto> Pets { get; init; } = [];
    public PetResponseDto? Pet { get; init; }
    public ArgumentException? CreateException { get; init; }
    public Exception? UpdateException { get; init; }
    public Guid? RequestedUserId { get; private set; }

    public Task<IReadOnlyList<PetResponseDto>> GetByUserIdAsync(Guid userId)
    {
        RequestedUserId = userId;
        return Task.FromResult(Pets);
    }

    public Task<PetResponseDto?> GetByIdAsync(Guid id, Guid userId)
    {
        RequestedUserId = userId;
        return Task.FromResult(Pet);
    }

    public Task<PetResponseDto> CreateAsync(CreatePetDto dto, Guid userId)
    {
        RequestedUserId = userId;
        return CreateException is null
            ? Task.FromResult(Pet ?? new PetResponseDto { Name = "Buddy" })
            : Task.FromException<PetResponseDto>(CreateException);
    }

    public Task<PetResponseDto> UpdateAsync(Guid id, Guid userId, UpdatePetDto dto)
    {
        RequestedUserId = userId;
        return UpdateException is null
            ? Task.FromResult(Pet ?? new PetResponseDto { Id = id, Name = "Buddy" })
            : Task.FromException<PetResponseDto>(UpdateException);
    }

    public Task<PetResponseDto> UpdatePhotoAsync(Guid id, Guid userId, IFormFile? photo) =>
        Task.FromResult(Pet ?? new PetResponseDto { Id = id, Name = "Buddy" });

    public Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        RequestedUserId = userId;
        return Task.FromResult(Pet is not null);
    }
}
