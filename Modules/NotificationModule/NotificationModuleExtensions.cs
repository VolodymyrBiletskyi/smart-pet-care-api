using smart_pet_care_api.Modules.NotificationModule.Config;
using smart_pet_care_api.Modules.NotificationModule.Domain;
using smart_pet_care_api.Modules.NotificationModule.Repository;

namespace smart_pet_care_api.Modules.NotificationModule
{
    public static class NotificationModuleExtensions
    {
        public static IServiceCollection AddNotificationModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<FirebaseOptions>(configuration.GetSection("Firebase"));

            services.AddSingleton<FirebaseInitializer>();
            services.AddSingleton<FcmRetryPolicy>();

            services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
            services.AddScoped<IDeviceTokenService, DeviceTokenService>();
            services.AddScoped<INotificationService, NotificationService>();

            return services;
        }
    }
}
