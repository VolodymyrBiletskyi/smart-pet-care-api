using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.UserModule.Mapper
{
    public static class UserMapper
    {
        public static UserResponseDto ToDto(this User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        public static User ToEntity(CreateUserDto dto, string passwordHash)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                PasswordHash = passwordHash,
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void PatchEntity(this User user, PatchUserDto patchDto)
        {
            if (patchDto.DisplayName != null) user.DisplayName = patchDto.DisplayName;
            if (patchDto.FirstName != null) user.FirstName = patchDto.FirstName;
            if (patchDto.LastName != null) user.LastName = patchDto.LastName;
            if (patchDto.PhoneNumber != null) user.PhoneNumber = patchDto.PhoneNumber;

            user.UpdatedAt = DateTime.UtcNow;
        }
    }
}