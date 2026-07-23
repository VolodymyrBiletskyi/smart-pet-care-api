using smart_pet_care_api.Modules.JournalModule.Domain;
using smart_pet_care_api.Modules.JournalModule.Repository;

namespace smart_pet_care_api.Modules.JournalModule
{
    public static class JournalModuleExtensions
    {
        public static IServiceCollection AddJournalModule(this IServiceCollection services)
        {
            services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
            services.AddScoped<IJournalEntryService, JournalEntryService>();
            return services;
        }
    }
}
