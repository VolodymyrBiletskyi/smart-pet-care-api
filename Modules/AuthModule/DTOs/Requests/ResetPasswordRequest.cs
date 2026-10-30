using smart_pet_care_api.Common.Api;
using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    /// <summary>
    /// The password rules are copied from <see cref="RegisterRequest"/> rather than
    /// relaxed: a reset that accepted a password the sign-up form rejects would be the
    /// way around them.
    /// </summary>
    public class ResetPasswordRequest : IValidatableObject
    {
        [Required(ErrorMessage = ErrorCodes.Field.ResetTokenRequired)]
        public string Token { get; set; } = null!;

        [Required(ErrorMessage = ErrorCodes.Field.PasswordRequired)]
        [MinLength(6, ErrorMessage = ErrorCodes.Field.PasswordTooShort)]
        [RegularExpression(
            @"^(?=.*[a-zA-Z])(?=.*\d)(?=.*[\W_]).+$",
            ErrorMessage = ErrorCodes.Field.PasswordTooWeak)]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = ErrorCodes.Field.PasswordConfirmRequired)]
        public string PasswordConfirm { get; set; } = null!;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Password != PasswordConfirm)
                yield return new ValidationResult(
                    ErrorCodes.Field.PasswordsDoNotMatch,
                    new[] { nameof(PasswordConfirm) });
        }
    }
}
