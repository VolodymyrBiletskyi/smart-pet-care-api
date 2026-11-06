using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        /// <summary>
        /// The session this token belongs to: one id handed down from the token a
        /// sign-in issued through every token that replaces it. Rotation on its own
        /// cannot tell a client asking twice from somebody replaying a stolen token, so
        /// a token presented after it was already spent revokes the whole family — that
        /// one session on that one device, and none of the user's others.
        /// </summary>
        public Guid TokenFamilyId { get; set; } = Guid.NewGuid();

        public string TokenHash { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// The token issued in this one's place, so a rotation chain can be read back
        /// out of the table. Null on the token a session currently holds, and on one
        /// revoked by a logout or a reset rather than replaced by a refresh.
        /// </summary>
        public string? ReplacedByTokenHash { get; set; }

        public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    }
}