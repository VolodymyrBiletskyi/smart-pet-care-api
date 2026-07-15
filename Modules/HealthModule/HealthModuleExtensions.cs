using smart_pet_care_api.Modules.HealthModule.Domain;
using smart_pet_care_api.Modules.HealthModule.Repository;

namespace smart_pet_care_api.Modules.HealthModule
{
    public static class HealthModuleExtensions
    {
        public static IServiceCollection AddHealthModule(this IServiceCollection services)
        {
            services.AddScoped<IHealthRecordRepository, HealthRecordRepository>();
            services.AddScoped<IHealthRecordService, HealthRecordService>();
            return services;
        }
    }
}
