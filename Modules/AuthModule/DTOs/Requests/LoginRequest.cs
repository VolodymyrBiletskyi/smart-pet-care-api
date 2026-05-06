using System.ComponentModel.DataAnnotations;


namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class LoginRequest
    {
        [Required]
        public string Email { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
    }
}