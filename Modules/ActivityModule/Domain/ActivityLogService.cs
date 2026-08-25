using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using smart_pet_care_api.Modules.ActivityModule.Mapper;
using smart_pet_care_api.Modules.ActivityModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Domain
{
    public class ActivityLogService : IActivityLogService
    {
        private const int MaxSteps = 1_000_000;
        private const int MaxLocationLength = 200;
        private const int MaxNoteLength = 2000;

        private readonly IActivityLogRepository _repo;
        private readonly IActivitySourceResolver _sources;

        public ActivityLogService(IActivityLogRepository repo, IActivitySourceResolver sources)
        {
            _repo = repo;
            _sources = sources;
        }

        public async Task<IReadOnlyList<ActivityLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null, ActivitySource? source = null)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            if (source.HasValue && !Enum.IsDefined(source.Value))
                throw new ArgumentException("Source is invalid");

            var fromUtc = from is { } f ? ActivityLogMapper.NormalizeToUtc(f) : (DateTime?)null;
            var toUtc = to is { } t ? ActivityLogMapper.NormalizeToUtc(t) : (DateTime?)null;

            if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
                throw new ArgumentException("From cannot be later than To");

            var logs = await _repo.GetByPetIdAsync(petId, fromUtc, toUtc, source);
            return logs.Select(log => log.ToDto()).ToList();
        }

        public async Task<ActivityLogResponseDto?> GetByIdAsync(Guid petId, Guid activityLogId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var log = await _repo.GetByIdAsync(activityLogId);
            if (log is null || log.PetId != petId) return null;

            return log.ToDto();
        }

        public async Task<ActivityLogResponseDto> CreateAsync(Guid petId, Guid userId, CreateActivityLogDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var source = dto.Source ?? ActivitySource.Manual;
            if (!Enum.IsDefined(source))
                throw new ArgumentException("Source is invalid");

            var provider = _sources.Resolve(source)
                ?? throw new ArgumentException($"Activity source {source} is not supported yet");

            var reading = await provider.ReadAsync(petId, dto);

            // Validated after the provider ran, so a device feed answers to the same rules
            // as a typed-in note rather than getting a free pass on its way to the database.
            ValidateReading(reading);

            var log = ActivityLogMapper.ToEntity(reading, petId, provider.Source);

            await _repo.AddAsync(log);
            await _repo.SaveChangesAsync();

            return log.ToDto();
        }

        public async Task<bool> DeleteAsync(Guid petId, Guid activityLogId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var log = await _repo.GetTrackedByIdAsync(activityLogId);
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

        private static void ValidateReading(ActivityReading reading)
        {
            if (ActivityLogMapper.NormalizeToUtc(reading.RecordedAt) > DateTime.UtcNow.AddMinutes(10))
                throw new ArgumentException("RecordedAt cannot be in the future");

            if (reading.Steps is { } steps)
            {
                if (steps < 0)
                    throw new ArgumentException("Steps cannot be negative");

                if (steps > MaxSteps)
                    throw new ArgumentException($"Steps must be {MaxSteps} or less");
            }

            if (reading.Location is { Length: > MaxLocationLength })
                throw new ArgumentException($"Location must be {MaxLocationLength} characters or less");

            if (reading.Note is { Length: > MaxNoteLength })
                throw new ArgumentException($"Note must be {MaxNoteLength} characters or less");

            // A row with no steps, no place and no text records nothing at all.
            if (reading.Steps is null && reading.Location is null && reading.Note is null)
                throw new ArgumentException("At least one of Steps, Location or Note is required");
        }
    }
}
