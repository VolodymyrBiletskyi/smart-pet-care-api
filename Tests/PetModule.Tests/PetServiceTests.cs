using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using smart_pet_care_api.Common.Patching;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetModule.Domain;
using smart_pet_care_api.Modules.PetModule.DTOs;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.PetModule.Tests;

public class PetServiceTests
{
    private readonly Guid userId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_MapsPetAddsInitialWeightLogAndSaves()
    {
        var repo = new FakePetRepository();
        var dto = ValidCreate();
        dto.Name = "  Buddy  ";
        dto.WeightKg = 12.3m;
        dto.Allergies = [" pollen ", " "];

        var result = await Service(repo).CreateAsync(dto, userId);

        Assert.NotNull(repo.AddedPet);
        Assert.Equal(userId, repo.AddedPet.UserId);
        Assert.Equal("Buddy", repo.AddedPet.Name);
        Assert.Equal(["pollen"], repo.AddedPet.Allergies);
        var log = Assert.Single(repo.AddedPet.WeightLogs);
        Assert.Equal(12.3m, log.WeightKg);
        Assert.Equal(repo.AddedPet.Id, log.PetId);
        Assert.Equal(1, repo.SaveChangesCalls);
        Assert.Equal(repo.AddedPet.Id, result.Id);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("230.01")]
    public async Task CreateAsync_WhenWeightIsOutsideRange_Throws(string rawWeight)
    {
        var dto = ValidCreate();
        dto.WeightKg = decimal.Parse(rawWeight, System.Globalization.CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(new FakePetRepository()).CreateAsync(dto, userId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public async Task CreateAsync_WhenSpeciesIsInvalid_Throws(int rawSpecies)
    {
        var dto = ValidCreate();
        dto.Species = (AnimalSpecies)rawSpecies;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(new FakePetRepository()).CreateAsync(dto, userId));

        Assert.Equal("Species is invalid", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenBirthDateIsFuture_ThrowsBeforeAdding()
    {
        var repo = new FakePetRepository();
        var dto = ValidCreate();
        dto.BirthDate = DateTime.UtcNow.Date.AddDays(1);

        await Assert.ThrowsAsync<ArgumentException>(() => Service(repo).CreateAsync(dto, userId));

        Assert.Null(repo.AddedPet);
    }

    [Fact]
    public async Task UpdateAsync_WhenPetIsNotOwned_ThrowsWithoutSaving()
    {
        var repo = new FakePetRepository();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(repo).UpdateAsync(Guid.NewGuid(), userId, new UpdatePetDto { Name = "Buddy" }));

        Assert.Equal("Pet does not exist", exception.Message);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_WhenPatchIsEmpty_ThrowsWithoutSaving()
    {
        var repo = new FakePetRepository { TrackedPet = Pet() };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).UpdateAsync(repo.TrackedPet.Id, userId, new UpdatePetDto()));

        Assert.Equal("At least one field must be provided", exception.Message);
        Assert.Null(repo.TrackedPet.UpdatedAt);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_WhenPhotoIsCleared_DeletesOldPhotoAfterSaving()
    {
        var repo = new FakePetRepository
        {
            TrackedPet = Pet(photoUrl: "old-url", publicId: "old-public-id")
        };
        var cloudinary = new FakeCloudinaryService(repo.Events);
        var dto = new UpdatePetDto { PhotoUrl = PatchField<string?>.Set(null) };

        var result = await Service(repo, cloudinary).UpdateAsync(repo.TrackedPet.Id, userId, dto);

        Assert.Null(result.PhotoUrl);
        Assert.Null(result.PhotoPublicId);
        Assert.Equal(["save", "delete-photo:old-public-id"], repo.Events);
    }

    [Fact]
    public async Task DeleteAsync_SavesBeforePhotoCleanupAndIgnoresCleanupFailure()
    {
        var repo = new FakePetRepository
        {
            TrackedPet = Pet(publicId: "old-public-id")
        };
        var cloudinary = new FakeCloudinaryService(repo.Events)
        {
            DeleteException = new InvalidOperationException("cloud unavailable")
        };

        var deleted = await Service(repo, cloudinary).DeleteAsync(repo.TrackedPet.Id, userId);

        Assert.True(deleted);
        Assert.Same(repo.TrackedPet, repo.DeletedPet);
        Assert.Equal(["delete-pet", "save", "delete-photo:old-public-id"], repo.Events);
    }

    [Fact]
    public async Task UpdatePhotoAsync_WhenSaveFails_DeletesNewUploadAndKeepsOldRemotePhoto()
    {
        var repo = new FakePetRepository
        {
            TrackedPet = Pet(publicId: "old-public-id"),
            SaveException = new InvalidOperationException("save failed")
        };
        var cloudinary = new FakeCloudinaryService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(repo, cloudinary).UpdatePhotoAsync(repo.TrackedPet.Id, userId, ValidPhoto()));

        Assert.Equal(["new-public-id"], cloudinary.DeletedPublicIds);
        Assert.Equal("pets/photos", cloudinary.UploadFolder);
    }

    [Fact]
    public async Task UpdatePhotoAsync_ReplacesPhotoThenDeletesOldRemotePhoto()
    {
        var repo = new FakePetRepository
        {
            TrackedPet = Pet(publicId: "old-public-id")
        };
        var cloudinary = new FakeCloudinaryService(repo.Events);

        var result = await Service(repo, cloudinary)
            .UpdatePhotoAsync(repo.TrackedPet.Id, userId, ValidPhoto());

        Assert.Equal("new-url", result.PhotoUrl);
        Assert.Equal("new-public-id", result.PhotoPublicId);
        Assert.Equal(["upload", "save", "delete-photo:old-public-id"], repo.Events);
    }

    private static PetService Service(
        FakePetRepository repo,
        FakeCloudinaryService? cloudinary = null) =>
        new(repo, cloudinary ?? new FakeCloudinaryService(), NullLogger<PetService>.Instance);

    private static CreatePetDto ValidCreate() => new()
    {
        Name = "Buddy",
        Species = AnimalSpecies.Dog,
        Sex = Sex.Male
    };

    private Pet Pet(string? photoUrl = null, string? publicId = null) => new()
    {
        UserId = userId,
        Name = "Buddy",
        Species = AnimalSpecies.Dog,
        PhotoUrl = photoUrl,
        PhotoPublicId = publicId
    };

    private static IFormFile ValidPhoto()
    {
        var stream = new MemoryStream([1]);
        return new FormFile(stream, 0, stream.Length, "photo", "pet.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }
}
