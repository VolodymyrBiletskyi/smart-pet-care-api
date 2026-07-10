using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class ConfirmEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits")]
        public string Code { get; set; } = null!;
    }
}
