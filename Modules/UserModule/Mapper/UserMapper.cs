using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;
using smart_pet_care_api.Modules.UserModule.Repository;

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
                GoogleAvatarUrl = user.AvatarUrl,
                CustomAvatarUrl = AvatarUrlFor(user.Id, user.AvatarData != null),
                TermsAccepted = user.TermsAccepted,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        /// <summary>
        /// Same response from the projected row. The avatar is published as a URL either
        /// way, so whether one exists is all the read path ever needed from the column.
        /// </summary>
        public static UserResponseDto ToDto(this UserProfileRow profile)
        {
            return new UserResponseDto
            {
                Id = profile.Id,
                Email = profile.Email,
                DisplayName = profile.DisplayName,
                PhoneNumber = profile.PhoneNumber,
                GoogleAvatarUrl = profile.AvatarUrl,
                CustomAvatarUrl = AvatarUrlFor(profile.Id, profile.HasAvatar),
                TermsAccepted = profile.TermsAccepted,
                CreatedAt = profile.CreatedAt,
                UpdatedAt = profile.UpdatedAt
            };
        }

        private static string? AvatarUrlFor(Guid userId, bool hasAvatar) =>
            hasAvatar ? $"/api/profile/avatar/{userId}" : null;

        public static void PatchEntity(this User user, PatchUserDto patchDto)
        {
            if (patchDto.DisplayName != null) user.DisplayName = patchDto.DisplayName;
            if (patchDto.PhoneNumber != null) user.PhoneNumber = patchDto.PhoneNumber;

            user.UpdatedAt = DateTime.UtcNow;
        }
    }
}