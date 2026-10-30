using System;

namespace smart_pet_care_api.Models
{
    /// <summary>
    /// One password reset link. Its own table rather than a purpose column on
    /// <see cref="EmailConfirmationCode"/>: the two are looked up differently --
    /// a code by user, a reset by the token in a URL -- and sharing the table
    /// would put a row that can change a password within one wrong predicate of
    /// the query that confirms an address.
    /// </summary>
    public class PasswordResetToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        // SHA256 of the token from the link; the raw value exists only in the email.
        public string TokenHash { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsActive => UsedAt == null && !IsExpired;
    }
}
