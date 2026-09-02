using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.Mapper;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Domain.Sources
{
    /// <summary>
    /// Hand-typed activity notes. The reading is the request body, normalized — no device,
    /// no network call.
    /// </summary>
    public class ManualActivitySourceProvider : IActivitySourceProvider
    {
        public ActivitySource Source => ActivitySource.Manual;

        public Task<ActivityReading> ReadAsync(Guid petId, CreateActivityLogDto dto)
        {
            var reading = new ActivityReading(
                ActivityLogMapper.NormalizeToUtc(dto.RecordedAt),
                dto.Steps,
                Trim(dto.Location),
                Trim(dto.Note),
                dto.Type,
                dto.Intensity,
                dto.DurationMinutes);

            return Task.FromResult(reading);
        }

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
