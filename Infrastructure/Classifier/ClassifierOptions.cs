namespace smart_pet_care_api.Infrastructure.Classifier;

public sealed class ClassifierOptions
{
    public const string SectionName = "Classifier";

    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 30;
    public int CircuitBreakerFailureThreshold { get; init; } = 5;
    public int CircuitBreakerBreakSeconds { get; init; } = 30;
}
