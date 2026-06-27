using Microsoft.AspNetCore.Http;

namespace smart_pet_care_api.Infrastructure.Cloudinary;

public interface ICloudinaryService
{
    Task<string> UploadImageAsync(IFormFile file, string folder);
}
