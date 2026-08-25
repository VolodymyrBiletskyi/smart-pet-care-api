using System.ComponentModel.DataAnnotations;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.WellnessModule.DTOs;

public sealed record WellnessRecalculationRequestDto
{
    [MaxLength(4000)]
    public string? CurrentSymptoms { get; init; }
}

public sealed record WellnessAssessmentResponseDto
{
    public required Guid AssessmentId { get; init; }
    public required Guid PetId { get; init; }
    public required ClassifierWellnessResponse Result { get; init; }
}

public sealed record WellnessHistoryResponseDto
{
    public required IReadOnlyList<WellnessAssessmentResponseDto> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
