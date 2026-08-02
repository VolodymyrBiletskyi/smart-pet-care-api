using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Infrastructure.Classifier;

public interface IClassifierClient
{
    Task<ClassifierChatResponse> ChatAsync(
        ClassifierChatRequest request,
        CancellationToken cancellationToken = default);

    Task<ClassifierNutritionResponse> AnalyzeNutritionAsync(
        ClassifierNutritionRequest request,
        CancellationToken cancellationToken = default);

    Task<ClassifierWellnessResponse> AnalyzeWellnessAsync(
        ClassifierWellnessRequest request,
        CancellationToken cancellationToken = default);
}
