using smart_pet_care_api.Common.Api;
using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class ConfirmEmailRequest
    {
        [Required(ErrorMessage = ErrorCodes.Field.EmailRequired)]
        [EmailAddress(ErrorMessage = ErrorCodes.Field.EmailInvalid)]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = ErrorCodes.Field.ConfirmationCodeRequired)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = ErrorCodes.Field.ConfirmationCodeMalformed)]
        public string Code { get; set; } = null!;
    }
}
