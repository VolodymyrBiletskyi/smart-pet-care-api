using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;
using smart_pet_care_api.Modules.UserModule.Mapper;
using smart_pet_care_api.Modules.UserModule.Repository;
using smart_pet_care_api.Modules.PetModule.Domain;
using smart_pet_care_api.Modules.PetModule.Repository;

namespace smart_pet_care_api.Modules.UserModule.Domain
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IPetRepository _petRepo;
        private readonly IPetPhotoCleanupService _petPhotoCleanupService;

        public UserService(
            IUserRepository userRepo,
            IPetRepository petRepo,
            IPetPhotoCleanupService petPhotoCleanupService)
        {
            _userRepo = userRepo;
            _petRepo = petRepo;
            _petPhotoCleanupService = petPhotoCleanupService;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null) return false;

            var photoPublicIds = await _petRepo.GetPhotoPublicIdsByUserIdAsync(id);

            await _userRepo.DeleteAsync(id);
            await _userRepo.SaveChangesAsync();

            await _petPhotoCleanupService.DeletePetPhotosBestEffortAsync(photoPublicIds);

            return true;
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

        public async Task<UserResponseDto> SaveAvatarAsync(Guid id, IFormFile? file)
        {
            ValidateAvatar(file);

            var user = await _userRepo.GetByIdAsync(id);
            if (user is null)
                throw new InvalidOperationException("User does not exist");

            using var ms = new MemoryStream();
            await file!.CopyToAsync(ms);
            var data = ms.ToArray();

            var contentType = ResolveImageContentType(data)
                ?? throw new ArgumentException("File content is not a valid JPEG, PNG, or WebP image");

            user.AvatarData = data;
            user.AvatarContentType = contentType;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.SaveChangesAsync();
            return user.ToDto();
        }

        private static void ValidateAvatar(IFormFile? file)
        {
            if (file is null || file.Length == 0)
                throw new ArgumentException("Photo is required");

            const long maxBytes = 1 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new ArgumentException("Photo must be 1 MB or less");
        }

        // The stored content type is derived from the file signature, not the
        // client-supplied Content-Type header, which cannot be trusted.
        private static string? ResolveImageContentType(byte[] data)
        {
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return "image/jpeg";

            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return "image/png";

            if (data.Length >= 12
                && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
                && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
                return "image/webp";

            return null;
        }

        public async Task<(byte[] Data, string ContentType)?> GetAvatarAsync(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user?.AvatarData == null) return null;
            return (user.AvatarData, user.AvatarContentType!);
        }
    }
}
