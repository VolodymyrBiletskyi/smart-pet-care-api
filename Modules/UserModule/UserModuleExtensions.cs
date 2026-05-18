using smart_pet_care_api.Modules.UserModule.Domain;
using smart_pet_care_api.Modules.UserModule.Repository;

namespace smart_pet_care_api.Modules.UserModule
{
    public static class UserModuleExtensions
    {
        public static IServiceCollection AddUserModule(this IServiceCollection services)
        {
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserService, UserService>();
            return services;
        }
    }
}
