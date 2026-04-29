using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.UserModule.Domain
{
    public interface IUserService
    {
        Task<IReadOnlyList<UserResponseDto>> GetAllAsync();
        Task<UserResponseDto> CreateAsync(CreateUserDto dto);
        Task<UserResponseDto?> GetByIdAsync(Guid id);
        Task<UserResponseDto> UpdateAsync(Guid id, PatchUserDto patchDto);
        Task<bool> DeleteAsync(Guid id);
    }
}