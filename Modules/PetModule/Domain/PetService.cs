using smart_pet_care_api.Modules.PetModule.DTOs;
using smart_pet_care_api.Modules.PetModule.Mapper;
using smart_pet_care_api.Modules.PetModule.Repository;

namespace smart_pet_care_api.Modules.PetModule.Domain
{
    public class PetService : IPetService
    {
        private readonly IPetRepository _petRepo;

        public PetService(IPetRepository petRepo)
        {
            _petRepo = petRepo;
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

        private static void ValidateCreate(CreatePetDto dto)
        {
            ValidateRequiredText(dto.Name, "Name");
            ValidateRequiredText(dto.Species, "Species");
            ValidateBirthDate(dto.BirthDate);
            ValidateWeight(dto.WeightKg);
        }

        private static void ValidateUpdate(UpdatePetDto dto)
        {
            ValidateOptionalText(dto.Name, "Name");
            ValidateOptionalText(dto.Species, "Species");
            ValidateBirthDate(dto.BirthDate);
            ValidateWeight(dto.WeightKg);
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
    }
}
