using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class RegisterRequest : IValidatableObject
    {
        [Required]
        [EmailAddress]
        [RegularExpression(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            ErrorMessage = "Email must contain domain like example.com")]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6)]
        [RegularExpression(
            @"^(?=.*[a-zA-Z])(?=.*\d)(?=.*[\W_]).+$",
            ErrorMessage = "Password must contain at least one letter, one number and one special character")]
        public string Password { get; set; } = null!;

        [Required]
        public string PasswordConfirm { get; set; } = null!;

        [Required]
        public bool TermsAccepted { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Password != PasswordConfirm)
                yield return new ValidationResult(
                    "Passwords do not match",
                    new[] { nameof(PasswordConfirm) });

            if (!TermsAccepted)
                yield return new ValidationResult(
                    "You must accept the terms and conditions",
                    new[] { nameof(TermsAccepted) });
        }
    }
}
