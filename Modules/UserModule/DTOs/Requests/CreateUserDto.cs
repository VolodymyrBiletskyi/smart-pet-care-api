using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Modules.UserModule.DTOs.Requests
{
    public class CreateUserDto
    {
        [Required]
        [EmailAddress]
        [RegularExpression(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        ErrorMessage = "Email must contain domain like example.com"
)]
        public string Email { get; set; } = null!;
        [Required]
        [MinLength(6)]
        [RegularExpression(
        @"^(?=.*[a-zA-Z])(?=.*\d)(?=.*[\W_]).+$",
        ErrorMessage = "Password must contain at least one letter, one number and one special character"
    )]
        public string Password { get; set; } = null!;
    }
}