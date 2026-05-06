using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Responses
{
    public class AuthTokenPair
    {
        public AuthResponse Auth { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}