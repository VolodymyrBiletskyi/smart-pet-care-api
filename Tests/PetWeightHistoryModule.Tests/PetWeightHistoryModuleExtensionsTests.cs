using Microsoft.Extensions.DependencyInjection;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Domain;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Repository;
using Xunit;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Tests;

public class PetWeightHistoryModuleExtensionsTests
{
    [Fact]
    public void AddPetWeightHistoryModule_RegistersScopedRepositoryAndService()
    {
        var services = new ServiceCollection();

        var result = services.AddPetWeightHistoryModule();

        Assert.Same(services, result);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPetWeightLogRepository) &&
            descriptor.ImplementationType == typeof(PetWeightLogRepository) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPetWeightLogService) &&
            descriptor.ImplementationType == typeof(PetWeightLogService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
