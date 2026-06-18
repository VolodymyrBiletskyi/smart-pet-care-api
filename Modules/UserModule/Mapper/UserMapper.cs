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
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                TermsAccepted = user.TermsAccepted,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        public static void PatchEntity(this User user, PatchUserDto patchDto)
        {
            if (patchDto.DisplayName != null) user.DisplayName = patchDto.DisplayName;
            if (patchDto.PhoneNumber != null) user.PhoneNumber = patchDto.PhoneNumber;

            user.UpdatedAt = DateTime.UtcNow;
        }
    }
}