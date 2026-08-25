using smart_pet_care_api.Modules.WellnessModule.Domain;

namespace smart_pet_care_api.Modules.WellnessModule;

public static class WellnessModuleExtensions
{
    public static IServiceCollection AddWellnessModule(this IServiceCollection services)
    {
        services.AddSingleton<WellnessCalculationLock>();
        services.AddScoped<IWellnessDataAggregator, WellnessDataAggregator>();
        services.AddScoped<IWellnessService, WellnessService>();
        return services;
    }
}
