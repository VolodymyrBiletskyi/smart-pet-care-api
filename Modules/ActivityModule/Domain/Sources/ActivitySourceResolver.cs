using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Domain.Sources
{
    public class ActivitySourceResolver : IActivitySourceResolver
    {
        private readonly IReadOnlyDictionary<ActivitySource, IActivitySourceProvider> _providers;

        public ActivitySourceResolver(IEnumerable<IActivitySourceProvider> providers)
        {
            var map = new Dictionary<ActivitySource, IActivitySourceProvider>();

            // Indexer rather than ToDictionary: two providers claiming the same source is a
            // substitution (last registration wins), not a crash at startup.
            foreach (var provider in providers)
                map[provider.Source] = provider;

            _providers = map;
        }

        public IActivitySourceProvider? Resolve(ActivitySource source) =>
            _providers.TryGetValue(source, out var provider) ? provider : null;
    }
}
