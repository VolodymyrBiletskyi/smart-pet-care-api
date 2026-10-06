using Microsoft.Extensions.DependencyInjection;
using smart_pet_care_api.Modules.NoteModule.Domain;
using smart_pet_care_api.Modules.NoteModule.Repository;
using Xunit;

namespace smart_pet_care_api.Modules.NoteModule.Tests;

public class NoteModuleExtensionsTests
{
    [Fact]
    public void AddNoteModule_RegistersScopedRepositoryAndService()
    {
        var services = new ServiceCollection();

        var result = services.AddNoteModule();

        Assert.Same(services, result);
        AssertScoped<INoteRepository, NoteRepository>(services);
        AssertScoped<INoteService, NoteService>(services);
    }

    private static void AssertScoped<TService, TImplementation>(IServiceCollection services) =>
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
}
