using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.FeedingModule.DTOs.Requests;
using smart_pet_care_api.Modules.FeedingModule.DTOs.Responses;
using smart_pet_care_api.Modules.FeedingModule.Mapper;
using smart_pet_care_api.Modules.FeedingModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.FeedingModule.Domain
{
    public class FeedingLogService : IFeedingLogService
    {
        private readonly IFeedingLogRepository _repo;

        public FeedingLogService(IFeedingLogRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<FeedingLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var logs = await _repo.GetByPetIdAsync(petId);
            return logs.Select(log => log.ToDto()).ToList();
        }

        public async Task<FeedingLogResponseDto?> GetByIdAsync(Guid petId, Guid logId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var log = await _repo.GetByIdAsync(logId);
            if (log is null || log.PetId != petId) return null;

            return log.ToDto();
        }

        public async Task<FeedingLogResponseDto> CreateAsync(Guid petId, Guid userId, CreateFeedingLogDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            ValidateCreate(dto);

            var log = FeedingLogMapper.ToEntity(dto, petId);
            await _repo.AddAsync(log);
            await _repo.SaveChangesAsync();

            return log.ToDto();
        }

        public async Task<FeedingLogResponseDto> UpdateAsync(Guid petId, Guid logId, Guid userId, PatchFeedingLogDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            ValidatePatch(dto);

            var log = await _repo.GetTrackedByIdAsync(logId);
            if (log is null || log.PetId != petId)
                throw new InvalidOperationException("Feeding log not found");

            log.PatchEntity(dto);
            ValidateFinalState(log);

            await _repo.SaveChangesAsync();

            return log.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid petId, Guid logId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var log = await _repo.GetTrackedByIdAsync(logId);
            if (log is null || log.PetId != petId) return false;

            _repo.Delete(log);
            await _repo.SaveChangesAsync();

            return true;
        }

        private async Task EnsurePetBelongsToUserAsync(Guid petId, Guid userId)
        {
            var petBelongsToUser = await _repo.PetBelongsToUserAsync(petId, userId);
            if (!petBelongsToUser)
                throw new InvalidOperationException("Pet not found");
        }

        private static void ValidateCreate(CreateFeedingLogDto dto)
        {
            ValidateFedAt(dto.FedAt);
            ValidateFoodType(dto.FoodType);
            ValidatePortionAmount(dto.PortionAmount);
            ValidateApproxCalories(dto.ApproxCalories);

            if (dto.PortionAmount.HasValue && !dto.PortionUnit.HasValue)
                throw new ArgumentException("PortionUnit is required when PortionAmount is specified");

            if (dto.PortionUnit.HasValue)
                ValidatePortionUnit(dto.PortionUnit.Value);
        }

        private static void ValidatePatch(PatchFeedingLogDto dto)
        {
            if (!dto.FedAt.IsSet
                && !dto.FoodType.IsSet
                && !dto.FoodName.IsSet
                && !dto.PortionAmount.IsSet
                && !dto.PortionUnit.IsSet
                && !dto.ApproxCalories.IsSet
                && !dto.Notes.IsSet)
            {
                throw new ArgumentException("At least one field must be provided");
            }

            if (dto.FedAt.IsSet) ValidateFedAt(dto.FedAt.Value);
            if (dto.FoodType.IsSet) ValidateFoodType(dto.FoodType.Value);
            if (dto.PortionAmount.IsSet) ValidatePortionAmount(dto.PortionAmount.Value);
            if (dto.ApproxCalories.IsSet) ValidateApproxCalories(dto.ApproxCalories.Value);
            if (dto.PortionUnit is { IsSet: true, Value: { } portionUnit }) ValidatePortionUnit(portionUnit);
        }

        private static void ValidateFinalState(FeedingLog log)
        {
            if (log.PortionAmount.HasValue && !log.PortionUnit.HasValue)
                throw new ArgumentException("PortionUnit is required when PortionAmount is specified");
        }

        private static void ValidateFedAt(DateTime? fedAt)
        {
            if (fedAt is null || fedAt.Value == default)
                throw new ArgumentException("FedAt is required");

            if (FeedingLogMapper.NormalizeToUtc(fedAt.Value) > DateTime.UtcNow.AddMinutes(10))
                throw new ArgumentException("FedAt cannot be more than 10 minutes in the future");
        }

        private static void ValidatePortionAmount(decimal? portionAmount)
        {
            if (portionAmount.HasValue && portionAmount.Value < 0)
                throw new ArgumentException("PortionAmount cannot be negative");
        }

        private static void ValidateApproxCalories(int? approxCalories)
        {
            if (approxCalories.HasValue && approxCalories.Value < 0)
                throw new ArgumentException("ApproxCalories cannot be negative");
        }

        private static void ValidateFoodType(FoodType foodType)
        {
            if (!Enum.IsDefined(foodType))
                throw new ArgumentException("FoodType is invalid");
        }

        private static void ValidatePortionUnit(PortionUnit portionUnit)
        {
            if (!Enum.IsDefined(portionUnit))
                throw new ArgumentException("PortionUnit is invalid");
        }
    }
}
