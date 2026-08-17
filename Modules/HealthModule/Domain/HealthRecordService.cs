using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.HealthModule.DTOs.Requests;
using smart_pet_care_api.Modules.HealthModule.DTOs.Responses;
using smart_pet_care_api.Modules.HealthModule.Mapper;
using smart_pet_care_api.Modules.HealthModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.Domain
{
    public class HealthRecordService : IHealthRecordService
    {
        private readonly IHealthRecordRepository _repo;
        private readonly IReminderRecalculationService _reminderRecalculation;

        public HealthRecordService(
            IHealthRecordRepository repo,
            IReminderRecalculationService reminderRecalculation)
        {
            _repo = repo;
            _reminderRecalculation = reminderRecalculation;
        }

        public async Task<IReadOnlyList<HealthRecordResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, HealthRecordType? type, SymptomType? symptom, DateTime? from, DateTime? to)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            if (type.HasValue) ValidateType(type.Value);
            if (symptom.HasValue) ValidateSymptom(symptom.Value);

            var fromUtc = from is { } f ? HealthRecordMapper.NormalizeToUtc(f) : (DateTime?)null;
            var toUtc = to is { } t ? HealthRecordMapper.NormalizeToUtc(t) : (DateTime?)null;

            if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
                throw new ArgumentException("From cannot be later than To");

            var records = await _repo.GetByPetIdAsync(petId, type, symptom, fromUtc, toUtc);
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

            // Second entry point into recalculation. A treatment can happen without any rule
            // behind it — vaccinated at the vet, nobody set anything up — and the next date is
            // still needed, so creating a record against a reminder moves that reminder on.
            // Registering the same completion twice is a no-op: the schedule is derived from
            // PerformedAt rather than shifted from wherever it currently sits.
            if (dto.ReminderId is { } reminderId)
            {
                var outcome = await _reminderRecalculation.RegisterCompletionAsync(
                    reminderId, record.PerformedAt, expectedPetId: petId)
                    ?? throw new InvalidOperationException("Reminder not found");

                // NextDueAt belongs to the server for linked records; letting the client set it
                // too would give us two sources of truth for one date.
                record.NextDueAt = outcome.Reminder.NextTriggerAt;
            }

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
            ValidateSymptoms(dto.Symptoms);
            ValidateDosage(dto.Dosage);
            ValidateProvider(dto.Provider);
        }

        private static void ValidatePatch(PatchHealthRecordDto dto)
        {
            if (dto.Type.IsSet) ValidateType(dto.Type.Value);
            if (dto.Title.IsSet) ValidateTitle(dto.Title.Value);
            if (dto.PerformedAt.IsSet) ValidatePerformedAt(dto.PerformedAt.Value);
            if (dto.Description.IsSet) ValidateDescription(dto.Description.Value);
            if (dto.Symptoms.IsSet) ValidateSymptoms(dto.Symptoms.Value);
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

        private static void ValidateSymptoms(List<SymptomType>? symptoms)
        {
            if (symptoms is null) return;
            foreach (var symptom in symptoms)
                ValidateSymptom(symptom);
        }

        private static void ValidateSymptom(SymptomType symptom)
        {
            if (!Enum.IsDefined(symptom))
                throw new ArgumentException("Symptom is invalid");
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
