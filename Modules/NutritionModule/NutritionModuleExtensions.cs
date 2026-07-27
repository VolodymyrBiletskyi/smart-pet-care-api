using smart_pet_care_api.Modules.NutritionModule.Domain;
using smart_pet_care_api.Modules.NutritionModule.Repository;

namespace smart_pet_care_api.Modules.NutritionModule
{
    public static class NutritionModuleExtensions
    {
        public static IServiceCollection AddNutritionModule(this IServiceCollection services)
        {
            services.AddScoped<INutritionGoalRepository, NutritionGoalRepository>();
            services.AddScoped<INutritionService, NutritionService>();
            return services;
        }
    }
}
