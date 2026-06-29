namespace smart_pet_care_api.Infrastructure.Cloudinary;

public class CloudinaryUploadException : Exception
{
    public CloudinaryUploadException(string message)
        : base(message)
    {
    }

    public CloudinaryUploadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
