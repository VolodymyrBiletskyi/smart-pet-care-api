using smart_pet_care_api.Modules.LegalModule.Domain;

namespace smart_pet_care_api.Modules.LegalModule
{
    public static class LegalModuleExtensions
    {
        public static IServiceCollection AddLegalModule(this IServiceCollection services)
        {
            // Singleton: the documents are read from disk once and held, so every
            // request after the first is served from memory.
            services.AddSingleton<ILegalDocumentProvider, LegalDocumentProvider>();
            return services;
        }
    }
}
