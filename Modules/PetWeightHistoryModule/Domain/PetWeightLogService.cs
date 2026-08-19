using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Responses;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Mapper;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.Domain;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Domain
{
    public class PetWeightLogService : IPetWeightLogService
    {
        private readonly IPetWeightLogRepository _repo;
        private readonly IReminderRecalculationService _reminderRecalculation;

        public PetWeightLogService(
            IPetWeightLogRepository repo,
            IReminderRecalculationService reminderRecalculation)
        {
            _repo = repo;
            _reminderRecalculation = reminderRecalculation;
        }

        public async Task<IReadOnlyList<PetWeightLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            var period = NormalizeAndValidatePeriod(from, to);

            var logs = await _repo.GetByPetIdAsync(petId, period.From, period.To);
            return logs.Select(log => log.ToDto()).ToList();
        }

        public async Task<PetWeightLogResponseDto> CreateAsync(Guid petId, Guid userId, CreatePetWeightLogDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            ValidateCreate(dto);

            var log = PetWeightLogMapper.ToEntity(dto, petId);
            await EnsureMeasuredAtIsUniqueAsync(petId, log.MeasuredAt);

            // The richer of the two ways to close a weighing reminder; /complete records the fact
            // alone. Re-registering one it already handled today is a no-op, so the log still
            // saves and the schedule does not move twice.
            if (dto.ReminderId is { } reminderId)
            {
                _ = await _reminderRecalculation.RegisterCompletionAsync(
                    reminderId, log.MeasuredAt, expectedPetId: petId)
                    ?? throw new InvalidOperationException("Reminder not found");
            }

            await RefreshPetCurrentWeightAsync(petId, log);
            await _repo.AddAsync(log);
            await _repo.SaveChangesAsync();

            return log.ToDto();
        }

        public async Task<PetWeightLogResponseDto> UpdateAsync(Guid petId, Guid weightLogId, Guid userId, PatchPetWeightLogDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            ValidatePatch(dto);

            var log = await _repo.GetTrackedByIdAsync(weightLogId);
            if (log is null || log.PetId != petId)
                throw new InvalidOperationException("Weight log not found");

            log.PatchEntity(dto);
            ValidateFinalState(log);
            await EnsureMeasuredAtIsUniqueAsync(petId, log.MeasuredAt, log.Id);

            await RefreshPetCurrentWeightAsync(petId, log, log.Id);
            await _repo.SaveChangesAsync();

            return log.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid petId, Guid weightLogId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var log = await _repo.GetTrackedByIdAsync(weightLogId);
            if (log is null || log.PetId != petId) return false;

            await RefreshPetCurrentWeightAsync(petId, excludeId: log.Id);
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

        private async Task EnsureMeasuredAtIsUniqueAsync(Guid petId, DateTime measuredAt, Guid? excludeId = null)
        {
            var exists = await _repo.ExistsForPetAtMeasuredAtAsync(petId, measuredAt, excludeId);
            if (exists)
                throw new PetWeightLogConflictException("A weight log for this pet already exists at the same measurement time.");
        }

        private async Task RefreshPetCurrentWeightAsync(
            Guid petId,
            PetWeightLog? pendingLog = null,
            Guid? excludeId = null)
        {
            var pet = await _repo.GetTrackedPetByIdAsync(petId);
            if (pet is null)
                throw new InvalidOperationException("Pet not found");

            var latestPersistedLog = await _repo.GetLatestByPetIdAsync(petId, excludeId);
            var latestLog = IsLaterThan(pendingLog, latestPersistedLog)
                ? pendingLog
                : latestPersistedLog;
            pet.WeightKg = latestLog?.WeightKg;
            pet.UpdatedAt = DateTime.UtcNow;
        }

        private static bool IsLaterThan(PetWeightLog? candidate, PetWeightLog? current)
        {
            if (candidate is null)
                return false;

            if (current is null)
                return true;

            return candidate.MeasuredAt > current.MeasuredAt
                || (candidate.MeasuredAt == current.MeasuredAt
                    && candidate.CreatedAt > current.CreatedAt);
        }

        private static void ValidateCreate(CreatePetWeightLogDto dto)
        {
            ValidateWeightKg(dto.WeightKg);
            ValidateMeasuredAt(dto.MeasuredAt);
            ValidateNotes(dto.Notes);
        }

        private static void ValidatePatch(PatchPetWeightLogDto dto)
        {
            if (!dto.WeightKg.IsSet && !dto.MeasuredAt.IsSet && !dto.Notes.IsSet)
                throw new ArgumentException("At least one field must be provided");

            if (dto.WeightKg.IsSet) ValidateWeightKg(dto.WeightKg.Value);
            if (dto.MeasuredAt.IsSet) ValidateMeasuredAt(dto.MeasuredAt.Value);
            if (dto.Notes.IsSet) ValidateNotes(dto.Notes.Value);
        }

        private static void ValidateFinalState(PetWeightLog log)
        {
            ValidateWeightKg(log.WeightKg);
            ValidateMeasuredAt(log.MeasuredAt);
            ValidateNotes(log.Notes);
        }

        private static void ValidateWeightKg(decimal weightKg)
        {
            if (weightKg <= 0)
                throw new ArgumentException("WeightKg must be greater than 0");

            if (weightKg > 230)
                throw new ArgumentException("WeightKg cannot be greater than 230");
        }

        private static void ValidateMeasuredAt(DateTime measuredAt)
        {
            ValidateMeasuredAt((DateTime?)measuredAt);
        }

        private static void ValidateMeasuredAt(DateTime? measuredAt)
        {
            if (!measuredAt.HasValue || measuredAt.Value == default)
                throw new ArgumentException("MeasuredAt is required");

            if (PetWeightLogMapper.NormalizeToUtc(measuredAt.Value) > DateTime.UtcNow.AddMinutes(10))
                throw new ArgumentException("MeasuredAt cannot be more than 10 minutes in the future");
        }

        private static (DateTime? From, DateTime? To) NormalizeAndValidatePeriod(DateTime? from, DateTime? to)
        {
            var normalizedFrom = from.HasValue
                ? PetWeightLogMapper.NormalizeToUtc(from.Value)
                : (DateTime?)null;

            var normalizedTo = to.HasValue
                ? NormalizePeriodEndToUtc(to.Value)
                : (DateTime?)null;

            if (normalizedFrom.HasValue && normalizedTo.HasValue && normalizedFrom.Value > normalizedTo.Value)
                throw new ArgumentException("From cannot be later than To");

            return (normalizedFrom, normalizedTo);
        }

        private static DateTime NormalizePeriodEndToUtc(DateTime to)
        {
            var normalized = PetWeightLogMapper.NormalizeToUtc(to);
            return normalized.TimeOfDay == TimeSpan.Zero
                ? normalized.Date.AddDays(1).AddTicks(-1)
                : normalized;
        }

        private static void ValidateNotes(string? notes)
        {
            if (notes is not null && string.IsNullOrWhiteSpace(notes))
                throw new ArgumentException("Notes cannot be whitespace only");
        }
    }
}
