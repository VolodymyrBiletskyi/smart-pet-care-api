using Microsoft.AspNetCore.Http;

namespace smart_pet_care_api.Infrastructure.Cloudinary;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder);
    Task DeleteImageAsync(string publicId);
}
