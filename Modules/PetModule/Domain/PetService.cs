using smart_pet_care_api.Models;
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
        private readonly ILogger<PetService> _logger;

        public PetService(
            IPetRepository petRepo,
            ICloudinaryService cloudinaryService,
            ILogger<PetService> logger)
        {
            _petRepo = petRepo;
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        public async Task<PetResponseDto> CreateAsync(CreatePetDto dto, Guid userId)
        {
            ValidateCreate(dto);

            var entity = PetMapper.ToEntity(dto, userId);
            AddInitialWeightLogIfNeeded(entity, dto.WeightKg);

            await _petRepo.AddAsync(entity);
            await _petRepo.SaveChangesAsync();

            return PetMapper.ToResponseDto(entity);
        }

        public async Task<bool> DeleteAsync(Guid id, Guid userId)
        {
            var pet = await _petRepo.GetTrackedByIdAndUserIdAsync(id, userId);
            if (pet == null) return false;

            var oldPhotoPublicId = pet.PhotoPublicId;

            _petRepo.Delete(pet);
            await _petRepo.SaveChangesAsync();

            await TryDeleteCloudinaryImageAsync(oldPhotoPublicId);

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

            var oldPhotoUrl = pet.PhotoUrl;
            var oldPhotoPublicId = pet.PhotoPublicId;
            var shouldDeleteOldPhoto = ShouldDeleteOldPhoto(dto, oldPhotoUrl, oldPhotoPublicId);

            PetMapper.UpdateEntity(pet, dto);

            if (dto.PhotoUrl.IsSet && dto.PhotoUrl.Value is null)
                pet.PhotoPublicId = null;

            await _petRepo.SaveChangesAsync();

            if (shouldDeleteOldPhoto)
                await TryDeleteCloudinaryImageAsync(oldPhotoPublicId);

            return PetMapper.ToResponseDto(pet);
        }

        public async Task<PetResponseDto> UpdatePhotoAsync(Guid id, Guid userId, IFormFile? photo)
        {
            ValidatePhoto(photo);

            var pet = await _petRepo.GetTrackedByIdAndUserIdAsync(id, userId);
            if (pet is null)
                throw new InvalidOperationException("Pet does not exist");

            var oldPhotoPublicId = pet.PhotoPublicId;
            var uploadResult = await _cloudinaryService.UploadImageAsync(photo!, "pets/photos");
            pet.PhotoUrl = uploadResult.Url;
            pet.PhotoPublicId = uploadResult.PublicId;
            pet.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _petRepo.SaveChangesAsync();
            }
            catch
            {
                await TryDeleteCloudinaryImageAsync(uploadResult.PublicId);
                throw;
            }

            await TryDeleteCloudinaryImageAsync(oldPhotoPublicId);

            return PetMapper.ToResponseDto(pet);
        }

        private async Task TryDeleteCloudinaryImageAsync(string? publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return;

            try
            {
                await _cloudinaryService.DeleteImageAsync(publicId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Cloudinary pet photo {PublicId}", publicId);
            }
        }

        private static bool ShouldDeleteOldPhoto(UpdatePetDto dto, string? oldPhotoUrl, string? oldPhotoPublicId)
        {
            if (string.IsNullOrWhiteSpace(oldPhotoPublicId))
                return false;

            if (!dto.PhotoUrl.IsSet)
                return false;

            return !string.Equals(dto.PhotoUrl.Value, oldPhotoUrl, StringComparison.Ordinal);
        }

        private static void ValidateCreate(CreatePetDto dto)
        {
            ValidateRequiredText(dto.Name, "Name");
            if (!dto.Species.HasValue)
                throw new ArgumentException("Species is required");
            ValidateSpecies(dto.Species.Value);
            ValidateBirthDate(dto.BirthDate);
            ValidateWeight(dto.WeightKg);
            ValidateSex(dto.Sex);
            ValidateOptionalText(dto.PhotoPublicId, "PhotoPublicId");
        }

        private static void ValidateUpdate(UpdatePetDto dto)
        {
            ValidateOptionalText(dto.Name, "Name");
            if (dto.Species.HasValue) ValidateSpecies(dto.Species.Value);
            ValidateBirthDate(dto.BirthDate);
            ValidateOptionalSex(dto.Sex);
            if (dto.PhotoUrl.IsSet) ValidateOptionalText(dto.PhotoUrl.Value, "PhotoUrl");
            if (dto.PhotoPublicId.IsSet) ValidateOptionalText(dto.PhotoPublicId.Value, "PhotoPublicId");
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

            if (weightKg.HasValue && weightKg.Value > 230)
                throw new ArgumentException("WeightKg cannot be greater than 230");
        }

        private static void AddInitialWeightLogIfNeeded(Pet pet, decimal? weightKg)
        {
            if (!weightKg.HasValue)
                return;

            var now = DateTime.UtcNow;
            pet.WeightLogs.Add(new PetWeightLog
            {
                PetId = pet.Id,
                WeightKg = weightKg.Value,
                MeasuredAt = now,
                CreatedAt = now
            });
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

        private static void ValidateSpecies(AnimalSpecies species)
        {
            if (species == AnimalSpecies.Unknown || !Enum.IsDefined(species))
                throw new ArgumentException("Species is invalid");
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
