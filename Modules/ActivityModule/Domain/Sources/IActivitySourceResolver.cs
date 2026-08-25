using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Domain.Sources
{
    public interface IActivitySourceResolver
    {
        /// <summary>
        /// Returns the provider registered for <paramref name="source"/>, or null when no
        /// provider is wired up for it — every source except Manual, for now.
        /// </summary>
        IActivitySourceProvider? Resolve(ActivitySource source);
    }
}
