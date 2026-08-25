using Microsoft.Extensions.DependencyInjection;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
using smart_pet_care_api.Modules.ActivityModule.Repository;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class ActivityModuleExtensionsTests
{
    [Fact]
    public void AddActivityModule_RegistersScopedRepositoryServiceAndManualSource()
    {
        var services = new ServiceCollection();

        var result = services.AddActivityModule();

        Assert.Same(services, result);
        AssertScoped<IActivityLogRepository, ActivityLogRepository>(services);
        AssertScoped<IActivityLogService, ActivityLogService>(services);
        AssertScoped<IActivitySourceProvider, ManualActivitySourceProvider>(services);
        AssertScoped<IActivitySourceResolver, ActivitySourceResolver>(services);
    }

    /// <summary>
    /// A device integration ships as one extra provider registration. Nothing else in the
    /// container changes, and the service keeps resolving through the same interface.
    /// </summary>
    [Fact]
    public void AddActivityModule_ResolvesEveryRegisteredProviderThroughTheResolver()
    {
        var services = new ServiceCollection();
        services.AddActivityModule();
        services.AddScoped<IActivitySourceProvider>(_ => new StubActivitySourceProvider(
            ActivitySource.Device,
            new ActivityReading(DateTime.UtcNow, 10, null, null)));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IActivitySourceResolver>();

        Assert.IsType<ManualActivitySourceProvider>(resolver.Resolve(ActivitySource.Manual));
        Assert.IsType<StubActivitySourceProvider>(resolver.Resolve(ActivitySource.Device));
        Assert.Null(resolver.Resolve(ActivitySource.GoogleFit));
    }

    private static void AssertScoped<TService, TImplementation>(IServiceCollection services) =>
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
}
