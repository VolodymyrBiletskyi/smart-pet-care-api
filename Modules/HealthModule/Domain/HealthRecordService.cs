using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.HealthModule.DTOs.Requests;
using smart_pet_care_api.Modules.HealthModule.DTOs.Responses;
using smart_pet_care_api.Modules.HealthModule.Mapper;
using smart_pet_care_api.Modules.HealthModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.Domain
{
    public class HealthRecordService : IHealthRecordService
    {
        private readonly IHealthRecordRepository _repo;

        public HealthRecordService(IHealthRecordRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<HealthRecordResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, HealthRecordType? type, DateTime? from, DateTime? to)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            if (type.HasValue) ValidateType(type.Value);

            var fromUtc = from is { } f ? HealthRecordMapper.NormalizeToUtc(f) : (DateTime?)null;
            var toUtc = to is { } t ? HealthRecordMapper.NormalizeToUtc(t) : (DateTime?)null;

            if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
                throw new ArgumentException("From cannot be later than To");

            var records = await _repo.GetByPetIdAsync(petId, type, fromUtc, toUtc);
            return records.Select(r => r.ToDto()).ToList();
        }

        public async Task<HealthRecordResponseDto?> GetByIdAsync(Guid petId, Guid recordId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var record = await _repo.GetByIdAsync(recordId);
            if (record is null || record.PetId != petId) return null;

            return record.ToDto();
        }

        public async Task<HealthRecordResponseDto> CreateAsync(Guid petId, Guid userId, CreateHealthRecordDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            ValidateCreate(dto);

            var record = HealthRecordMapper.ToEntity(dto, petId);
            ValidateFinalState(record);

            await _repo.AddAsync(record);
            await _repo.SaveChangesAsync();

            return record.ToDto();
        }

        public async Task<HealthRecordResponseDto> UpdateAsync(Guid petId, Guid recordId, Guid userId, PatchHealthRecordDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            ValidatePatch(dto);

            var record = await _repo.GetTrackedByIdAsync(recordId);
            if (record is null || record.PetId != petId)
                throw new InvalidOperationException("Health record not found");

            record.PatchEntity(dto);
            ValidateFinalState(record);

            await _repo.SaveChangesAsync();

            return record.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid petId, Guid recordId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var record = await _repo.GetTrackedByIdAsync(recordId);
            if (record is null || record.PetId != petId) return false;

            _repo.Delete(record);
            await _repo.SaveChangesAsync();

            return true;
        }

        private async Task EnsurePetBelongsToUserAsync(Guid petId, Guid userId)
        {
            var petBelongsToUser = await _repo.PetBelongsToUserAsync(petId, userId);
            if (!petBelongsToUser)
                throw new InvalidOperationException("Pet not found");
        }

        private static void ValidateCreate(CreateHealthRecordDto dto)
        {
            ValidateType(dto.Type);
            ValidateTitle(dto.Title);
            ValidatePerformedAt(dto.PerformedAt);
            ValidateDescription(dto.Description);
            ValidateDosage(dto.Dosage);
            ValidateProvider(dto.Provider);
        }

        private static void ValidatePatch(PatchHealthRecordDto dto)
        {
            if (dto.Type.IsSet) ValidateType(dto.Type.Value);
            if (dto.Title.IsSet) ValidateTitle(dto.Title.Value);
            if (dto.PerformedAt.IsSet) ValidatePerformedAt(dto.PerformedAt.Value);
            if (dto.Description.IsSet) ValidateDescription(dto.Description.Value);
            if (dto.Dosage.IsSet) ValidateDosage(dto.Dosage.Value);
            if (dto.Provider.IsSet) ValidateProvider(dto.Provider.Value);
        }

        private static void ValidateFinalState(HealthRecord record)
        {
            if (record.NextDueAt.HasValue && record.NextDueAt.Value < record.PerformedAt)
                throw new ArgumentException("NextDueAt cannot be earlier than PerformedAt");
        }

        private static void ValidateType(HealthRecordType type)
        {
            if (!Enum.IsDefined(type))
                throw new ArgumentException("Type is invalid");
        }

        private static void ValidateTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required");

            if (title.Trim().Length > 200)
                throw new ArgumentException("Title must be 200 characters or less");
        }

        private static void ValidatePerformedAt(DateTime performedAt)
        {
            if (HealthRecordMapper.NormalizeToUtc(performedAt) > DateTime.UtcNow.AddMinutes(10))
                throw new ArgumentException("PerformedAt cannot be in the future");
        }

        private static void ValidateDescription(string? description)
        {
            if (description is { Length: > 2000 })
                throw new ArgumentException("Description must be 2000 characters or less");
        }

        private static void ValidateDosage(string? dosage)
        {
            if (dosage is { Length: > 200 })
                throw new ArgumentException("Dosage must be 200 characters or less");
        }

        private static void ValidateProvider(string? provider)
        {
            if (provider is { Length: > 200 })
                throw new ArgumentException("Provider must be 200 characters or less");
        }
    }
}
