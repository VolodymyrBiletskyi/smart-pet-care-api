using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;
using smart_pet_care_api.Modules.UserModule.Mapper;
using smart_pet_care_api.Modules.UserModule.Repository;

namespace smart_pet_care_api.Modules.UserModule.Domain
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        public UserService(IUserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null) return false;

            await _userRepo.DeleteAsync(id);
            await _userRepo.SaveChangesAsync();
            return true;
        }

        public async Task<IReadOnlyList<UserResponseDto>> GetAllAsync()
        {
            var users = await _userRepo.GetAllAsync();
            return users.Select(UserMapper.ToDto).ToList();
        }

        public async Task<UserResponseDto?> GetByIdAsync(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            return user is null ? null : UserMapper.ToDto(user);
        }

        public async Task<UserResponseDto> UpdateAsync(Guid id, PatchUserDto patchDto)
        {
            var existingUser = await _userRepo.GetByIdAsync(id);
            if (existingUser is null)
                throw new InvalidOperationException("User does not exist");

            existingUser.PatchEntity(patchDto);

            await _userRepo.SaveChangesAsync();
            return existingUser.ToDto();
        }

        public async Task<UserResponseDto> SaveAvatarAsync(Guid id, byte[] data, string contentType)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user is null)
                throw new InvalidOperationException("User does not exist");

            user.AvatarData = data;
            user.AvatarContentType = contentType;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.SaveChangesAsync();
            return user.ToDto();
        }

        public async Task<(byte[] Data, string ContentType)?> GetAvatarAsync(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user?.AvatarData == null) return null;
            return (user.AvatarData, user.AvatarContentType!);
        }
    }
}