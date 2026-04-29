using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Modules.UserModule.DTOs.Responses
{
    public class UserResponseDto
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}