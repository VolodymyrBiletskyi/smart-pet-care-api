using smart_pet_care_api.Modules.FeedingModule.Domain;
using smart_pet_care_api.Modules.FeedingModule.Repository;

namespace smart_pet_care_api.Modules.FeedingModule
{
    public static class FeedingModuleExtensions
    {
        public static IServiceCollection AddFeedingModule(this IServiceCollection services)
        {
            services.AddScoped<IFeedingLogRepository, FeedingLogRepository>();
            services.AddScoped<IFeedingLogService, FeedingLogService>();
            return services;
        }
    }
}
