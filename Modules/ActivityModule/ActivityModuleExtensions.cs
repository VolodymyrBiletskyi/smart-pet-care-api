using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
using smart_pet_care_api.Modules.ActivityModule.Repository;

namespace smart_pet_care_api.Modules.ActivityModule
{
    public static class ActivityModuleExtensions
    {
        public static IServiceCollection AddActivityModule(this IServiceCollection services)
        {
            services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
            services.AddScoped<IActivityLogService, ActivityLogService>();

            // Manual entry is the only source for now. A collar integration registers its own
            // IActivitySourceProvider here and needs no change below this line.
            services.AddScoped<IActivitySourceProvider, ManualActivitySourceProvider>();
            services.AddScoped<IActivitySourceResolver, ActivitySourceResolver>();

            return services;
        }
    }
}
