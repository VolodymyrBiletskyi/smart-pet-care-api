using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Modules.AuthModule.DTOs.Requests
{
    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = null!;
    }
}