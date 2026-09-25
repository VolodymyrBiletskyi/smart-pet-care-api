using smart_pet_care_api.Common.Api;
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
                throw new NotFoundException(ErrorCodes.PetNotFound, "Pet not found");

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
                throw new NotFoundException(ErrorCodes.PetNotFound, "Pet not found");

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
            ValidateRequiredText(dto.Name, "Name", ErrorCodes.Pet.NameRequired);
            if (!dto.Species.HasValue)
                throw new ValidationException(ErrorCodes.Pet.SpeciesRequired, "Species is required");
            ValidateSpecies(dto.Species.Value);
            ValidateBirthDate(dto.BirthDate);
            ValidateWeight(dto.WeightKg);
            ValidateSex(dto.Sex);
            ValidateOptionalText(dto.PhotoPublicId, "PhotoPublicId", ErrorCodes.Pet.PhotoPublicIdEmpty);
        }

        private static void ValidateUpdate(UpdatePetDto dto)
        {
            if (dto.Name is null
                && !dto.Species.HasValue
                && dto.Breed is null
                && !dto.BirthDate.HasValue
                && !dto.Sex.HasValue
                && !dto.PhotoUrl.IsSet
                && !dto.PhotoPublicId.IsSet
                && !dto.Allergies.IsSet
                && !dto.ChronicConditions.IsSet
                && !dto.BehavioralNotes.IsSet)
            {
                throw new ValidationException(
                    ErrorCodes.Pet.UpdateEmpty, "At least one field must be provided");
            }

            ValidateOptionalText(dto.Name, "Name", ErrorCodes.Pet.NameEmpty);
            if (dto.Species.HasValue) ValidateSpecies(dto.Species.Value);
            ValidateBirthDate(dto.BirthDate);
            ValidateOptionalSex(dto.Sex);
            if (dto.PhotoUrl.IsSet)
                ValidateOptionalText(dto.PhotoUrl.Value, "PhotoUrl", ErrorCodes.Pet.PhotoUrlEmpty);
            if (dto.PhotoPublicId.IsSet)
                ValidateOptionalText(dto.PhotoPublicId.Value, "PhotoPublicId", ErrorCodes.Pet.PhotoPublicIdEmpty);
        }

        private static void ValidateRequiredText(string? value, string fieldName, string code)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ValidationException(code, $"{fieldName} is required");
        }

        private static void ValidateOptionalText(string? value, string fieldName, string code)
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
                throw new ValidationException(code, $"{fieldName} cannot be empty");
        }


        private static void ValidateBirthDate(DateTime? birthDate)
        {
            if (birthDate.HasValue && birthDate.Value.Date > DateTime.UtcNow.Date)
                throw new ValidationException(
                    ErrorCodes.Pet.BirthDateInFuture, "BirthDate cannot be in the future");
        }

        private const decimal MaximumWeightKg = 230;

        private static void ValidateWeight(decimal? weightKg)
        {
            if (weightKg.HasValue && weightKg.Value <= 0)
                throw new ValidationException(
                    ErrorCodes.Pet.WeightNotPositive, "WeightKg must be greater than zero");

            if (weightKg.HasValue && weightKg.Value > MaximumWeightKg)
                throw new ValidationException(
                    ErrorCodes.Pet.WeightTooLarge,
                    $"WeightKg cannot be greater than {MaximumWeightKg}",
                    new Dictionary<string, object?> { ["max"] = MaximumWeightKg });
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
                throw new ValidationException(ErrorCodes.Pet.SexInvalid, "Sex is invalid");
        }

        private static void ValidateSpecies(AnimalSpecies species)
        {
            if (species == AnimalSpecies.Unknown || !Enum.IsDefined(species))
                throw new ValidationException(ErrorCodes.Pet.SpeciesInvalid, "Species is invalid");
        }

        private static void ValidatePhoto(IFormFile? photo)
        {
            if (photo is null || photo.Length == 0)
                throw new ValidationException(ErrorCodes.Pet.PhotoRequired, "Photo is required");

            var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg",
                "image/png",
                "image/webp"
            };

            if (!allowedContentTypes.Contains(photo.ContentType))
                throw new ValidationException(
                    ErrorCodes.Pet.PhotoTypeInvalid,
                    "Photo must be a JPEG, PNG, or WEBP image",
                    new Dictionary<string, object?> { ["allowed"] = allowedContentTypes.ToArray() });

            const int maxMegabytes = 5;
            if (photo.Length > maxMegabytes * 1024 * 1024)
                throw new ValidationException(
                    ErrorCodes.Pet.PhotoTooLarge,
                    $"Photo size must be {maxMegabytes}MB or less",
                    new Dictionary<string, object?> { ["maxMegabytes"] = maxMegabytes });
        }
    }
}

