using smart_pet_care_api.Modules.ReminderModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.Scheduler;

namespace smart_pet_care_api.Modules.ReminderModule
{
    public static class ReminderModuleExtensions
    {
        public static IServiceCollection AddReminderModule(this IServiceCollection services)
        {
            services.AddScoped<IReminderRepository, ReminderRepository>();
            services.AddScoped<IReminderService, ReminderService>();
            services.AddHostedService<ReminderSchedulerService>();
            return services;
        }
    }
}
