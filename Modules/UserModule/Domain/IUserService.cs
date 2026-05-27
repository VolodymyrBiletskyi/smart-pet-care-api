using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.UserModule.Domain
{
    public interface IUserService
    {
        Task<IReadOnlyList<UserResponseDto>> GetAllAsync();
        Task<UserResponseDto?> GetByIdAsync(Guid id);
        Task<UserResponseDto> UpdateAsync(Guid id, PatchUserDto patchDto);
        Task<bool> DeleteAsync(Guid id);
    }
}