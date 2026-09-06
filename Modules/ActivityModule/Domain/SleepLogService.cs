using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using smart_pet_care_api.Modules.ActivityModule.Mapper;
using smart_pet_care_api.Modules.ActivityModule.Repository;

namespace smart_pet_care_api.Modules.ActivityModule.Domain
{
    public class SleepLogService : ISleepLogService
    {
        private const decimal MaxHoursPerDay = 24m;
        private const int MaxNoteLength = 2000;

        private readonly ISleepLogRepository _repo;

        public SleepLogService(ISleepLogRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<SleepLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var fromDate = from is { } f ? SleepLogMapper.NormalizeToDate(f) : (DateTime?)null;
            var toDate = to is { } t ? SleepLogMapper.NormalizeToDate(t) : (DateTime?)null;

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
                throw new ArgumentException("From cannot be later than To");

            var logs = await _repo.GetByPetIdAsync(petId, fromDate, toDate);
            return logs.Select(log => log.ToDto()).ToList();
        }

        public async Task<SleepLogResponseDto?> GetByIdAsync(Guid petId, Guid sleepLogId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var log = await _repo.GetByIdAsync(sleepLogId);
            if (log is null || log.PetId != petId) return null;

            return log.ToDto();
        }

        public async Task<SleepLogResponseDto> CreateAsync(Guid petId, Guid userId, CreateSleepLogDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var log = SleepLogMapper.ToEntity(dto, petId);

            Validate(log);
            await EnsureDayFitsAsync(log);

            await _repo.AddAsync(log);
            await _repo.SaveChangesAsync();

            return log.ToDto();
        }

        public async Task<SleepLogResponseDto> UpdateAsync(Guid petId, Guid sleepLogId, Guid userId, PatchSleepLogDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            EnsurePatchHasFields(dto);

            var log = await _repo.GetTrackedByIdAsync(sleepLogId);
            if (log is null || log.PetId != petId)
                throw new InvalidOperationException("Sleep log not found");

            log.PatchEntity(dto);

            Validate(log);
            await EnsureDayFitsAsync(log, excludeSelf: true);

            await _repo.SaveChangesAsync();

            return log.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid petId, Guid sleepLogId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var log = await _repo.GetTrackedByIdAsync(sleepLogId);
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

        private static void EnsurePatchHasFields(PatchSleepLogDto dto)
        {
            if (!dto.SleepDate.IsSet && !dto.Hours.IsSet && !dto.Note.IsSet)
                throw new ArgumentException("At least one field must be provided");
        }

        private static void Validate(SleepLog log)
        {
            if (log.SleepDate > SleepLogMapper.NormalizeToDate(DateTime.UtcNow))
                throw new ArgumentException("SleepDate cannot be in the future");

            if (log.Hours <= 0)
                throw new ArgumentException("Hours must be greater than zero");

            if (log.Hours > MaxHoursPerDay)
                throw new ArgumentException($"Hours must be {MaxHoursPerDay} or less");

            if (log.Note is { Length: > MaxNoteLength })
                throw new ArgumentException($"Note must be {MaxNoteLength} characters or less");
        }

        /// <summary>
        /// A day can be logged as several naps, so the ceiling is on their sum. It catches the
        /// mistake a unique index would have caught — the same night entered twice — without
        /// forbidding the nap-by-nap logging a collar feed will want.
        /// </summary>
        private async Task EnsureDayFitsAsync(SleepLog log, bool excludeSelf = false)
        {
            var alreadyLogged = await _repo.GetLoggedHoursAsync(
                log.PetId,
                log.SleepDate,
                excludeSelf ? log.Id : null);

            if (alreadyLogged + log.Hours > MaxHoursPerDay)
                throw new ArgumentException(
                    $"Sleep for {log.SleepDate:yyyy-MM-dd} would total more than {MaxHoursPerDay} hours ({alreadyLogged} already logged)");
        }
    }
}
