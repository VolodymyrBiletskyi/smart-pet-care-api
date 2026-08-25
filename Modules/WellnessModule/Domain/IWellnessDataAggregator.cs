using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.WellnessModule.Domain;

public interface IWellnessDataAggregator
{
    Task<ClassifierWellnessRequest> AggregateAsync(
        Guid petId,
        Guid userId,
        string? currentSymptoms,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);
}
