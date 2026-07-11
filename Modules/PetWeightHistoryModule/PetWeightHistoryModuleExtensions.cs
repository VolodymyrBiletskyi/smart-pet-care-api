using smart_pet_care_api.Modules.PetWeightHistoryModule.Domain;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Repository;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule
{
    public static class PetWeightHistoryModuleExtensions
    {
        public static IServiceCollection AddPetWeightHistoryModule(this IServiceCollection services)
        {
            services.AddScoped<IPetWeightLogRepository, PetWeightLogRepository>();
            services.AddScoped<IPetWeightLogService, PetWeightLogService>();
            return services;
        }
    }
}
