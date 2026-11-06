using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Email { get; set; } = null!;
        public string? PasswordHash { get; set; }
        public string? DisplayName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? AvatarUrl { get; set; }
        public byte[]? AvatarData { get; set; }
        public string? AvatarContentType { get; set; }

        public bool TermsAccepted { get; set; } = false;
        public DateTime? TermsAcceptedAt { get; set; }

        public bool EmailConfirmed { get; set; } = false;
        public DateTime? EmailConfirmedAt { get; set; }

        /// <summary>
        /// Failed confirmation-code guesses, and when the window holding them started.
        /// On the account rather than on the code: per code the ceiling was five guesses
        /// and a fresh code with a fresh counter was sixty seconds away, so the budget it
        /// was meant to cap was in fact unbounded.
        /// </summary>
        public int ConfirmationAttempts { get; set; }
        public DateTime? ConfirmationAttemptsStartedAt { get; set; }

        /// <summary>
        /// Access tokens issued before this instant are refused by <c>AuthMiddleware</c>.
        /// Revoking refresh tokens ends a session's ability to continue, but the access
        /// token already in hand stays signed and valid for up to its full fifteen
        /// minutes and nothing about a JWT is looked up when it is used. Signing out and
        /// resetting a password both move this forward, which is what makes either take
        /// effect at once.
        /// </summary>
        public DateTime? TokensValidFrom { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Pet> Pets { get; set; } = new List<Pet>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();
    }
}
