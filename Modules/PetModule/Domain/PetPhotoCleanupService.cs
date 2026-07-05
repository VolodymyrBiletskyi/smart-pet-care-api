using smart_pet_care_api.Infrastructure.Cloudinary;

namespace smart_pet_care_api.Modules.PetModule.Domain;

public class PetPhotoCleanupService : IPetPhotoCleanupService
{
    private readonly ICloudinaryService _cloudinaryService;
    private readonly ILogger<PetPhotoCleanupService> _logger;

    public PetPhotoCleanupService(
        ICloudinaryService cloudinaryService,
        ILogger<PetPhotoCleanupService> logger)
    {
        _cloudinaryService = cloudinaryService;
        _logger = logger;
    }

    public async Task DeletePetPhotosBestEffortAsync(IEnumerable<string?> photoPublicIds)
    {
        var distinctPublicIds = photoPublicIds
            .Where(publicId => !string.IsNullOrWhiteSpace(publicId))
            .Select(publicId => publicId!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var publicId in distinctPublicIds)
        {
            try
            {
                await _cloudinaryService.DeleteImageAsync(publicId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Cloudinary pet photo {PublicId}", publicId);
            }
        }
    }
}
