using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record SessionMessageResponseDto
{
    public required string Answer { get; init; }
    public ClassifierUrgency? Urgency { get; init; }
    public IReadOnlyList<string> HomeAdvice { get; init; } = [];
    public required string Disclaimer { get; init; }
    public bool UrgentContactEmergencyVet { get; init; }

    public static SessionMessageResponseDto FromClassifier(
        ClassifierChatResponse response)
    {
        var prediction = response.Mode == ClassifierChatMode.General
            ? null
            : response.Prediction;

        return new SessionMessageResponseDto
        {
            Answer = response.Answer,
            Urgency = prediction?.Urgency,
            HomeAdvice = prediction?.HomeAdvice ?? [],
            Disclaimer = response.Disclaimer,
            UrgentContactEmergencyVet = response.Mode == ClassifierChatMode.Emergency
        };
    }
}
