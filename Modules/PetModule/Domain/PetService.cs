using smart_pet_care_api.Infrastructure.Cloudinary;
using smart_pet_care_api.Modules.PetModule.DTOs;
using smart_pet_care_api.Modules.PetModule.Mapper;
using smart_pet_care_api.Modules.PetModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.PetModule.Domain
{
    public class PetService : IPetService
    {
        private readonly IPetRepository _petRepo;
        private readonly ICloudinaryService _cloudinaryService;

        public PetService(IPetRepository petRepo, ICloudinaryService cloudinaryService)
        {
            _petRepo = petRepo;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<PetResponseDto> CreateAsync(CreatePetDto dto, Guid userId)
        {
            ValidateCreate(dto);

            var entity = PetMapper.ToEntity(dto, userId);

            await _petRepo.AddAsync(entity);
            await _petRepo.SaveChangesAsync();

            return PetMapper.ToResponseDto(entity);
        }

        public async Task<bool> DeleteAsync(Guid id, Guid userId)
        {
            var pet = await _petRepo.GetTrackedByIdAndUserIdAsync(id, userId);
            if (pet == null) return false;

            _petRepo.Delete(pet);
            await _petRepo.SaveChangesAsync();

            return true;
        }

        public async Task<PetResponseDto?> GetByIdAsync(Guid id, Guid userId)
        {
            var pet = await _petRepo.GetByIdAndUserIdAsync(id, userId);
            return pet is null ? null : PetMapper.ToResponseDto(pet);
        }

        public async Task<IReadOnlyList<PetResponseDto>> GetByUserIdAsync(Guid userId)
        {
            var pets = await _petRepo.GetByUserIdAsync(userId);
            return pets.Select(PetMapper.ToResponseDto).ToList();
        }

        public async Task<PetResponseDto> UpdateAsync(Guid id, Guid userId, UpdatePetDto dto)
        {
            ValidateUpdate(dto);

            var pet = await _petRepo.GetTrackedByIdAndUserIdAsync(id, userId);
            if (pet is null)
                throw new InvalidOperationException("Pet does not exist");

            PetMapper.UpdateEntity(pet, dto);

            await _petRepo.SaveChangesAsync();

            return PetMapper.ToResponseDto(pet);
        }

        public async Task<PetResponseDto> UpdatePhotoAsync(Guid id, Guid userId, IFormFile? photo)
        {
            ValidatePhoto(photo);

            var pet = await _petRepo.GetTrackedByIdAndUserIdAsync(id, userId);
            if (pet is null)
                throw new InvalidOperationException("Pet does not exist");

            var photoUrl = await _cloudinaryService.UploadImageAsync(photo!, "pets/photos");
            pet.PhotoUrl = photoUrl;
            pet.UpdatedAt = DateTime.UtcNow;

            await _petRepo.SaveChangesAsync();

            return PetMapper.ToResponseDto(pet);
        }

        private static void ValidateCreate(CreatePetDto dto)
        {
            ValidateRequiredText(dto.Name, "Name");
            ValidateRequiredText(dto.Species, "Species");
            ValidateBirthDate(dto.BirthDate);
            ValidateWeight(dto.WeightKg);
            ValidateSex(dto.Sex);
        }

        private static void ValidateUpdate(UpdatePetDto dto)
        {
            ValidateOptionalText(dto.Name, "Name");
            ValidateOptionalText(dto.Species, "Species");
            ValidateBirthDate(dto.BirthDate);
            ValidateWeight(dto.WeightKg);
            ValidateOptionalSex(dto.Sex);
        }

        private static void ValidateRequiredText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{fieldName} is required");
        }

        private static void ValidateOptionalText(string? value, string fieldName)
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{fieldName} cannot be empty");
        }

        private static void ValidateBirthDate(DateTime? birthDate)
        {
            if (birthDate.HasValue && birthDate.Value.Date > DateTime.UtcNow.Date)
                throw new ArgumentException("BirthDate cannot be in the future");
        }

        private static void ValidateWeight(decimal? weightKg)
        {
            if (weightKg.HasValue && weightKg.Value <= 0)
                throw new ArgumentException("WeightKg must be greater than zero");
        }

        private static void ValidateOptionalSex(Sex? sex)
        {
            if (sex.HasValue)
                ValidateSex(sex.Value);
        }

        private static void ValidateSex(Sex sex)
        {
            if (!Enum.IsDefined(sex))
                throw new ArgumentException("Sex is invalid");
        }

        private static void ValidatePhoto(IFormFile? photo)
        {
            if (photo is null || photo.Length == 0)
                throw new ArgumentException("Photo is required");

            var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg",
                "image/png",
                "image/webp"
            };

            if (!allowedContentTypes.Contains(photo.ContentType))
                throw new ArgumentException("Photo must be a JPEG, PNG, or WEBP image");

            const long maxBytes = 5 * 1024 * 1024;
            if (photo.Length > maxBytes)
                throw new ArgumentException("Photo size must be 5MB or less");
        }
    }
}
