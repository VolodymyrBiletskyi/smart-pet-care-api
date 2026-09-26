using smart_pet_care_api.Common.Api;
using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class ResendConfirmationRequest
    {
        [Required(ErrorMessage = ErrorCodes.Field.EmailRequired)]
        [EmailAddress(ErrorMessage = ErrorCodes.Field.EmailInvalid)]
        public string Email { get; set; } = null!;
    }
}
