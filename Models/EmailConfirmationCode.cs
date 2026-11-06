using System;

namespace smart_pet_care_api.Models
{
    public class EmailConfirmationCode
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        // SHA256 of the 6-digit code; the plain code is only ever emailed.
        public string CodeHash { get; set; } = null!;

        /// <summary>
        /// The bcrypt hash of the password the sign-up that produced this code supplied,
        /// applied to the account when — and only when — this code is used. Null on a
        /// code for an account that already holds its registrant's password: the first
        /// sign-up for an address, and every sign-up that is not contested.
        /// <para>
        /// It is here rather than on the user because a second sign-up for an
        /// unconfirmed address used to overwrite the password on the row directly. The
        /// code still went to the mailbox, so the address's real owner confirmed an
        /// account whose password somebody else had chosen. Binding the password to the
        /// code means the party who proves the mailbox decides which sign-up wins.
        /// </para>
        /// </summary>
        public string? PendingPasswordHash { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// Failed guesses that arrived while this code was live. Diagnostic only — a
        /// wrong guess cannot be attributed to one of several live codes, and the
        /// ceiling that actually stops guessing is <c>User.ConfirmationAttempts</c>.
        /// </summary>
        public int Attempts { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsActive => UsedAt == null && !IsExpired;
    }
}
