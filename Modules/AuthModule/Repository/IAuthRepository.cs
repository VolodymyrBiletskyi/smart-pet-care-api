using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.AuthModule.Repository
{
    public interface IAuthRepository
    {
        Task<RefreshToken?> GetByHashAsync(string tokenHash);
        Task AddAsync(RefreshToken token);
        Task<List<RefreshToken>> GetActiveByUserAsync(Guid userId);
        Task<int> SaveChangesAsync();
    }
}