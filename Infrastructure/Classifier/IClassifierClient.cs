using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Infrastructure.Classifier;

public interface IClassifierClient
{
    Task<ClassifierChatResponse> ChatAsync(
        ClassifierChatRequest request,
        CancellationToken cancellationToken = default);
}
