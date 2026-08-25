using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Domain.Sources
{
    /// <summary>
    /// Turns a create request into an <see cref="ActivityReading"/>. The MVP ships a single
    /// implementation that reads the numbers straight off the request body; a collar
    /// integration adds a second one that calls the device API instead, and nothing in
    /// <see cref="ActivityLogService"/> has to change for it.
    /// </summary>
    public interface IActivitySourceProvider
    {
        /// <summary>The source this provider is registered under; the resolver keys on it.</summary>
        ActivitySource Source { get; }

        Task<ActivityReading> ReadAsync(Guid petId, CreateActivityLogDto dto);
    }
}
