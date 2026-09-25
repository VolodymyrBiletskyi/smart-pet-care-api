using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.WellnessModule.DTOs;

public sealed record WellnessResponseDto
{
    public int? WellnessScore { get; init; }
    public ClassifierWellnessBand? Band { get; init; }
    public required ClassifierWellnessScoreStatus ScoreStatus { get; init; }
    public required WellnessStatesDto States { get; init; }
    public required string Narrative { get; init; }
    public required IReadOnlyList<string> Recommendations { get; init; }
    public required IReadOnlyList<WellnessReminderSuggestionDto> ReminderSuggestions { get; init; }
    public required string Disclaimer { get; init; }
}

public sealed record WellnessStatesDto
{
    public required ClassifierWellnessReasonCode Activity { get; init; }
    public required ClassifierWellnessReasonCode Sleep { get; init; }
    public required ClassifierWellnessReasonCode Diet { get; init; }
    public required ClassifierWellnessReasonCode Symptoms { get; init; }
    public required ClassifierWellnessReasonCode PreventiveCare { get; init; }
    public required ClassifierWellnessReasonCode Baseline { get; init; }
}

public sealed record WellnessReminderSuggestionDto
{
    public required ClassifierWellnessReminderType Type { get; init; }
    public required string Text { get; init; }
}

public sealed record WellnessHistoryResponseDto
{
    public required IReadOnlyList<WellnessResponseDto> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
