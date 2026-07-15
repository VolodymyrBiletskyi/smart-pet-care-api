using System.Text.Json.Serialization;

namespace smart_pet_care_api.Infrastructure.Classifier.Contracts;

public sealed record ClassifierChatRequest
{
    public string? SessionId { get; init; }
    public required IReadOnlyList<ClassifierChatMessage> Messages { get; init; }
    public string? SymptomSummary { get; init; }
    public ClassifierPetType? PetType { get; init; }
}

public sealed record ClassifierChatMessage
{
    public required ClassifierChatRole Role { get; init; }
    public required string Content { get; init; }
}

public sealed record ClassifierChatResponse
{
    public required ClassifierChatMode Mode { get; init; }
    public required string Answer { get; init; }
    public required string SymptomSummary { get; init; }
    public ClassifierChatPrediction? Prediction { get; init; }
    public IReadOnlyList<string> RelatedTopics { get; init; } = [];
    public bool NeedsClarification { get; init; }
    public required string Disclaimer { get; init; }
}

public sealed record ClassifierErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required bool Retryable { get; init; }
    public int? RetryAfterSeconds { get; init; }
}

public sealed record ClassifierChatPrediction
{
    public required string PredictedCondition { get; init; }
    public required double Confidence { get; init; }
    public IReadOnlyList<ClassifierTopPrediction> TopK { get; init; } = [];
    public required ClassifierUrgency Urgency { get; init; }
    public required string Specialist { get; init; }
    public required string DiseaseCategory { get; init; }
    public IReadOnlyList<string> HomeAdvice { get; init; } = [];
}

public sealed record ClassifierTopPrediction
{
    public required string Condition { get; init; }
    public required double Confidence { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierChatRole>))]
public enum ClassifierChatRole
{
    [JsonStringEnumMemberName("user")]
    User,
    [JsonStringEnumMemberName("assistant")]
    Assistant
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierChatMode>))]
public enum ClassifierChatMode
{
    [JsonStringEnumMemberName("general")]
    General,
    [JsonStringEnumMemberName("health")]
    Health,
    [JsonStringEnumMemberName("emergency")]
    Emergency
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierUrgency>))]
public enum ClassifierUrgency
{
    [JsonStringEnumMemberName("MONITOR")]
    Monitor,
    [JsonStringEnumMemberName("CONSULT_SOON")]
    ConsultSoon,
    [JsonStringEnumMemberName("URGENT")]
    Urgent,
    [JsonStringEnumMemberName("EMERGENCY")]
    Emergency
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierPetType>))]
public enum ClassifierPetType
{
    [JsonStringEnumMemberName("dog")]
    Dog,
    [JsonStringEnumMemberName("cat")]
    Cat,
    [JsonStringEnumMemberName("rabbit")]
    Rabbit,
    [JsonStringEnumMemberName("hamster")]
    Hamster,
    [JsonStringEnumMemberName("guinea_pig")]
    GuineaPig,
    [JsonStringEnumMemberName("bird")]
    Bird,
    [JsonStringEnumMemberName("fish")]
    Fish,
    [JsonStringEnumMemberName("turtle")]
    Turtle,
    [JsonStringEnumMemberName("other")]
    Other
}
