using System;

namespace smart_pet_care_api.Models
{
    public class EmailConfirmationCode
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        // SHA256 of the 6-digit code; the plain code is only ever emailed.
        public string CodeHash { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }

        // Failed verification attempts against this code.
        public int Attempts { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsActive => UsedAt == null && !IsExpired;
    }
}
