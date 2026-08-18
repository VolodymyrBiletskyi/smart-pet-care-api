using Microsoft.AspNetCore.Http;
using smart_pet_care_api.Infrastructure.Cloudinary;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetModule.Repository;

namespace smart_pet_care_api.Modules.PetModule.Tests;

internal sealed class FakePetRepository : IPetRepository
{
    public IReadOnlyList<Pet> Pets { get; set; } = [];
    public Pet? PetById { get; set; }
    public Pet? TrackedPet { get; set; }
    public Pet? AddedPet { get; private set; }
    public Pet? DeletedPet { get; private set; }
    public Exception? SaveException { get; set; }
    public int SaveChangesCalls { get; private set; }
    public List<string> Events { get; } = [];

    public Task<IReadOnlyList<Pet>> GetByUserIdAsync(Guid userId) => Task.FromResult(Pets);
    public Task<IReadOnlyList<string?>> GetPhotoPublicIdsByUserIdAsync(Guid userId) =>
        Task.FromResult<IReadOnlyList<string?>>(Pets.Select(pet => pet.PhotoPublicId).ToList());
    public Task<Pet?> GetByIdAsync(Guid id) => Task.FromResult(PetById);
    public Task<Pet?> GetByIdAndUserIdAsync(Guid id, Guid userId) => Task.FromResult(PetById);
    public Task<Pet?> GetTrackedByIdAndUserIdAsync(Guid id, Guid userId) => Task.FromResult(TrackedPet);
    public Task<bool> ExistsForUserAsync(Guid id, Guid userId) =>
        Task.FromResult(PetById is not null || TrackedPet is not null);

    public Task<Pet> AddAsync(Pet entity)
    {
        AddedPet = entity;
        return Task.FromResult(entity);
    }

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        Events.Add("save");
        return SaveException is null
            ? Task.FromResult(1)
            : Task.FromException<int>(SaveException);
    }

    public void Delete(Pet pet)
    {
        DeletedPet = pet;
        Events.Add("delete-pet");
    }
}

internal sealed class FakeCloudinaryService : ICloudinaryService
{
    private readonly List<string>? events;

    public FakeCloudinaryService(List<string>? events = null)
    {
        this.events = events;
    }

    public CloudinaryUploadResult UploadResult { get; set; } = new("new-url", "new-public-id");
    public Exception? UploadException { get; set; }
    public Exception? DeleteException { get; set; }
    public IFormFile? UploadedFile { get; private set; }
    public string? UploadFolder { get; private set; }
    public List<string> DeletedPublicIds { get; } = [];

    public Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder)
    {
        UploadedFile = file;
        UploadFolder = folder;
        events?.Add("upload");
        return UploadException is null
            ? Task.FromResult(UploadResult)
            : Task.FromException<CloudinaryUploadResult>(UploadException);
    }

    public Task DeleteImageAsync(string publicId)
    {
        DeletedPublicIds.Add(publicId);
        events?.Add($"delete-photo:{publicId}");
        return DeleteException is null
            ? Task.CompletedTask
            : Task.FromException(DeleteException);
    }
}
