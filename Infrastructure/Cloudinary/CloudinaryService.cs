using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace smart_pet_care_api.Infrastructure.Cloudinary;

public class CloudinaryService : ICloudinaryService
{
    private readonly CloudinaryOptions _options;

    public CloudinaryService(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Image file is required.", nameof(file));

        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Cloudinary folder is required.", nameof(folder));

        await using var stream = file.OpenReadStream();
        var cloudinary = CreateClient();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder.Trim(),
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        };

        ImageUploadResult result;
        try
        {
            result = await cloudinary.UploadAsync(uploadParams);
        }
        catch (Exception ex)
        {
            throw new CloudinaryUploadException("Cloudinary upload failed.", ex);
        }

        if (result.Error is not null)
            throw new CloudinaryUploadException(result.Error.Message);

        return result.SecureUrl?.ToString()
            ?? throw new CloudinaryUploadException("Cloudinary did not return a secure image URL.");
    }

    private CloudinaryDotNet.Cloudinary CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.CloudName) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new CloudinaryUploadException("Cloudinary configuration is missing.");
        }

        var account = new Account(
            _options.CloudName.Trim(),
            _options.ApiKey.Trim(),
            _options.ApiSecret.Trim());

        return new CloudinaryDotNet.Cloudinary(account)
        {
            Api =
            {
                Secure = true
            }
        };
    }
}
