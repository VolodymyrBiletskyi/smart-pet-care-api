using smart_pet_care_api.Modules.PetModule.Domain;
using smart_pet_care_api.Modules.PetModule.Repository;

namespace smart_pet_care_api.Modules.PetModule
{
    public static class PetModuleExtensions
    {
        public static IServiceCollection AddPetModule(this IServiceCollection services)
        {
            services.AddScoped<IPetRepository, PetRepository>();
            services.AddScoped<IPetService, PetService>();
            return services;
        }
    }
}
