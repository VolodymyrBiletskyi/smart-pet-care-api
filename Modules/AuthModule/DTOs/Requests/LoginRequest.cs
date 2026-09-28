using smart_pet_care_api.Common.Api;
using System.ComponentModel.DataAnnotations;


namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class LoginRequest
    {
        [Required(ErrorMessage = ErrorCodes.Field.EmailRequired)]
        public string Email { get; set; } = null!;
        [Required(ErrorMessage = ErrorCodes.Field.PasswordRequired)]
        public string Password { get; set; } = null!;
    }
}