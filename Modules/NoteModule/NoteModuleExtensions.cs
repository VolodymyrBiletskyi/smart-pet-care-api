using smart_pet_care_api.Modules.NoteModule.Domain;
using smart_pet_care_api.Modules.NoteModule.Repository;

namespace smart_pet_care_api.Modules.NoteModule
{
    public static class NoteModuleExtensions
    {
        public static IServiceCollection AddNoteModule(this IServiceCollection services)
        {
            services.AddScoped<INoteRepository, NoteRepository>();
            services.AddScoped<INoteService, NoteService>();
            return services;
        }
    }
}
