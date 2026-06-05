using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Responses
{
    public class AuthResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string AccessToken { get; set; } = null!;
        public DateTime ExpiresAtUtc { get; set; }
        public string RefreshToken { get; set; } = null!;
    }
}