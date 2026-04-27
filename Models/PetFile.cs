using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class PetFile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public Guid UploadedByUserId { get; set; }

        public string StorageKey { get; set; } = null!;

        public string? FileUrl { get; set; }

        public string FileName { get; set; } = null!;
        public FileType Type { get; set; }

        public string ContentType { get; set; } = null!;
        public long Size { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}