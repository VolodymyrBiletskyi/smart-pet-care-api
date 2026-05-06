using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.UserModule.Repository
{
    public interface IUserRepository
    {
        Task<IReadOnlyList<User>> GetAllAsync();
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<User> AddAsync(User entity);
        Task<int> SaveChangesAsync();
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid userId);
        Task<ExternalLogin?> GetExternalLoginAsync(AuthProvider provider, string providerUserId);
        Task AddExternalLoginAsync(ExternalLogin login);

    }
}