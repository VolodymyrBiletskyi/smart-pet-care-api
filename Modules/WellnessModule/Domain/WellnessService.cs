using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.WellnessModule.DTOs;

namespace smart_pet_care_api.Modules.WellnessModule.Domain;

public sealed class WellnessService(
    AppDbContext dbContext,
    IWellnessDataAggregator aggregator,
    IClassifierClient classifierClient,
    WellnessCalculationLock calculationLock,
    TimeProvider timeProvider) : IWellnessService
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<WellnessResponseDto> RecalculateAsync(
        Guid petId,
        Guid userId,
        string? currentSymptoms,
        CancellationToken cancellationToken = default)
    {
        if (currentSymptoms is { Length: > 4000 })
            throw new ArgumentException("CurrentSymptoms cannot exceed 4000 characters");

        using var lease = await calculationLock.AcquireAsync(petId, cancellationToken);
        var evaluatedAt = timeProvider.GetUtcNow();
        var request = await aggregator.AggregateAsync(
            petId, userId, currentSymptoms, evaluatedAt, cancellationToken);
        var response = await classifierClient.CalculateWellnessAsync(request, cancellationToken);

        var assessment = new PetWellnessAssessment
        {
            PetId = petId,
            WellnessScore = response.WellnessScore,
            Band = response.Band?.ToString(),
            ScoreStatus = response.ScoreStatus.ToString(),
            DataCoverage = response.DataCoverage,
            CalculationVersion = response.CalculationVersion,
            EvaluatedAt = response.EvaluatedAt.UtcDateTime,
            WindowStartDate = response.EvaluationWindow?.StartDate,
            WindowEndDate = response.EvaluationWindow?.EndDate,
            Trend = response.Trend?.ToString(),
            ResponseJson = JsonSerializer.Serialize(response, SerializerOptions),
            CreatedAt = evaluatedAt.UtcDateTime
        };

        dbContext.PetWellnessAssessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(response);
    }

    public async Task<WellnessResponseDto?> GetCurrentAsync(
        Guid petId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePetBelongsToUserAsync(petId, userId, cancellationToken);
        var assessment = await dbContext.PetWellnessAssessments.AsNoTracking()
            .Where(item => item.PetId == petId)
            .OrderByDescending(item => item.EvaluatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return assessment is null ? null : ToDto(assessment);
    }

    public async Task<WellnessHistoryResponseDto> GetHistoryAsync(
        Guid petId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentException("Page must be at least 1");
        if (pageSize is < 1 or > MaximumPageSize)
            throw new ArgumentException($"PageSize must be between 1 and {MaximumPageSize}");

        await EnsurePetBelongsToUserAsync(petId, userId, cancellationToken);
        var query = dbContext.PetWellnessAssessments.AsNoTracking()
            .Where(item => item.PetId == petId);
        var totalCount = await query.CountAsync(cancellationToken);
        var assessments = await query.OrderByDescending(item => item.EvaluatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new WellnessHistoryResponseDto
        {
            Items = assessments.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task EnsurePetBelongsToUserAsync(
        Guid petId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Pets.AsNoTracking()
            .AnyAsync(item => item.Id == petId && item.UserId == userId, cancellationToken))
            throw new InvalidOperationException("Pet not found");
    }

    private static WellnessResponseDto ToDto(PetWellnessAssessment assessment) =>
        ToDto(JsonSerializer.Deserialize<ClassifierWellnessResponse>(
            assessment.ResponseJson, SerializerOptions)
            ?? throw new InvalidOperationException("Stored wellness assessment is invalid"));

    private static WellnessResponseDto ToDto(ClassifierWellnessResponse response) => new()
    {
        WellnessScore = response.WellnessScore,
        Band = response.Band,
        ScoreStatus = response.ScoreStatus,
        States = new WellnessStatesDto
        {
            Activity = PrimaryReason(response.Breakdown.Activity),
            Sleep = PrimaryReason(response.Breakdown.Sleep),
            Diet = PrimaryReason(response.Breakdown.Diet),
            Symptoms = PrimaryReason(response.Breakdown.Symptoms),
            PreventiveCare = PrimaryReason(response.Breakdown.PreventiveCare),
            Baseline = PrimaryReason(response.Breakdown.Baseline)
        },
        Narrative = response.Narrative,
        Recommendations = response.Recommendations,
        ReminderSuggestions = MapReminderSuggestions(response),
        Disclaimer = response.Disclaimer
    };

    private static ClassifierWellnessReasonCode PrimaryReason(
        ClassifierWellnessBreakdownItem item) =>
        item.ReasonCodes.Count > 0
            ? item.ReasonCodes[0]
            : throw new InvalidOperationException("Stored wellness assessment has a state without a reason code");

    private static IReadOnlyList<WellnessReminderSuggestionDto> MapReminderSuggestions(
        ClassifierWellnessResponse response) =>
        response.Reminders
            .Select(item => new WellnessReminderSuggestionDto
            {
                Type = item.Reminder,
                Text = item.Text
            })
            .Concat(response.TrackingRecommendations.SelectMany(recommendation =>
                recommendation.SuggestedReminderTypes.Select(type => new WellnessReminderSuggestionDto
                {
                    Type = type,
                    Text = recommendation.Text
                })))
            .DistinctBy(item => (item.Type, item.Text))
            .ToList();
}
