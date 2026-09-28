using smart_pet_care_api.Common.Api;
using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class RegisterRequest : IValidatableObject
    {
        [Required(ErrorMessage = ErrorCodes.Field.EmailRequired)]
        [EmailAddress(ErrorMessage = ErrorCodes.Field.EmailInvalid)]
        [RegularExpression(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            ErrorMessage = ErrorCodes.Field.EmailInvalid)]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = ErrorCodes.Field.PasswordRequired)]
        [MinLength(6, ErrorMessage = ErrorCodes.Field.PasswordTooShort)]
        [RegularExpression(
            @"^(?=.*[a-zA-Z])(?=.*\d)(?=.*[\W_]).+$",
            ErrorMessage = ErrorCodes.Field.PasswordTooWeak)]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = ErrorCodes.Field.PasswordConfirmRequired)]
        public string PasswordConfirm { get; set; } = null!;

        [Required(ErrorMessage = ErrorCodes.Field.TermsNotAccepted)]
        public bool TermsAccepted { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Password != PasswordConfirm)
                yield return new ValidationResult(
                    ErrorCodes.Field.PasswordsDoNotMatch,
                    new[] { nameof(PasswordConfirm) });

            if (!TermsAccepted)
                yield return new ValidationResult(
                    ErrorCodes.Field.TermsNotAccepted,
                    new[] { nameof(TermsAccepted) });
        }
    }
}
