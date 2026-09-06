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
        private const int MaxDurationMinutes = 1440;

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

            var log = ActivityLogMapper.ToEntity(ResolveIntensity(reading), petId, provider.Source);

            await _repo.AddAsync(log);
            await _repo.SaveChangesAsync();

            return log.ToDto();
        }

        public async Task<ActivityLogResponseDto> UpdateAsync(Guid petId, Guid activityLogId, Guid userId, PatchActivityLogDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);
            EnsurePatchHasFields(dto);

            var log = await _repo.GetTrackedByIdAsync(activityLogId);
            if (log is null || log.PetId != petId)
                throw new InvalidOperationException("Activity log not found");

            log.PatchEntity(dto);

            // Validated as a whole rather than field by field: the rules that matter are about
            // the row that results — "a log has to record something" cannot be checked against
            // a single cleared field. No provider runs here; an edit is the user's own words.
            ValidateReading(log.ToReading());

            if (log.DurationMinutes is not null && log.Intensity is null)
                log.Intensity = ActivityEffort.DefaultIntensityFor(log.Type);

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

        private static void EnsurePatchHasFields(PatchActivityLogDto dto)
        {
            if (!dto.RecordedAt.IsSet
                && !dto.Steps.IsSet
                && !dto.Type.IsSet
                && !dto.Intensity.IsSet
                && !dto.DurationMinutes.IsSet
                && !dto.Location.IsSet
                && !dto.Note.IsSet)
            {
                throw new ArgumentException("At least one field must be provided");
            }
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

            if (reading.Type is { } type && !Enum.IsDefined(type))
                throw new ArgumentException("Type is invalid");

            if (reading.Intensity is { } intensity && !Enum.IsDefined(intensity))
                throw new ArgumentException("Intensity is invalid");

            if (reading.DurationMinutes is { } duration)
            {
                if (duration <= 0)
                    throw new ArgumentException("DurationMinutes must be greater than zero");

                // One log is one session. A longer span is a day's worth of them and belongs
                // in as many rows, or the intensity of the whole stretch is a fiction.
                if (duration > MaxDurationMinutes)
                    throw new ArgumentException($"DurationMinutes must be {MaxDurationMinutes} or less");
            }

            // A row with no activity, no duration, no steps, no place and no text records
            // nothing at all.
            if (reading.Steps is null && reading.Location is null && reading.Note is null
                && reading.Type is null && reading.DurationMinutes is null)
                throw new ArgumentException("At least one of Type, DurationMinutes, Steps, Location or Note is required");
        }

        /// <summary>
        /// A duration with no intensity weighs nothing and drops out of the score, so one is
        /// filled in from the activity type. Making the field required instead would buy a
        /// number the user picked to get past the form.
        /// </summary>
        private static ActivityReading ResolveIntensity(ActivityReading reading) =>
            reading.DurationMinutes is null || reading.Intensity is not null
                ? reading
                : reading with { Intensity = ActivityEffort.DefaultIntensityFor(reading.Type) };
    }
}
