using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class ExternalLogin
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }

        public AuthProvider Provider { get; set; }
        public string ProviderUserId { get; set; } = null!;
        public string? ProviderEmail { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }


}