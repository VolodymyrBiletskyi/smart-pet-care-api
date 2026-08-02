using System.Text.Json.Serialization;

namespace smart_pet_care_api.Infrastructure.Classifier.Contracts;

/// <summary>
/// The <c>feeding-summary</c> route grades several pets in one call. The API
/// analyses one pet at a time, so <see cref="Pets"/> carries a single entry.
/// </summary>
public sealed record ClassifierFeedingSummaryRequest
{
    public required IReadOnlyList<ClassifierFeedingSummaryPet> Pets { get; init; }
}

public sealed record ClassifierFeedingSummaryPet
{
    /// <summary>Correlates the result back to the pet; the classifier echoes it.</summary>
    public required string PetId { get; init; }

    public ClassifierPetType Species { get; init; } = ClassifierPetType.Dog;
    public string? Breed { get; init; }

    /// <summary>Required by the classifier, and must be above 0 and at most 500.</summary>
    public required decimal WeightKg { get; init; }

    /// <summary>0 to 600. Selects the growth-energy multiplier below 12 months.</summary>
    public int? AgeMonths { get; init; }

    public IReadOnlyList<ClassifierFeedingProduct> Products { get; init; } = [];
}

public sealed record ClassifierFeedingProduct
{
    public required string Name { get; init; }
    public required decimal Calories { get; init; }
}

public sealed record ClassifierFeedingSummaryResponse
{
    public required IReadOnlyList<ClassifierFeedingSummaryResult> Results { get; init; }
    public required string Disclaimer { get; init; }
}

public sealed record ClassifierFeedingSummaryResult
{
    public required string PetId { get; init; }
    public required ClassifierFeedingStatus Status { get; init; }

    /// <summary>Daily calorie need the classifier derived from the pet's body data.</summary>
    public required decimal TargetCalories { get; init; }

    /// <summary>Calories in the supplied products.</summary>
    public required decimal ActualCalories { get; init; }

    /// <summary>Signed percentage away from <see cref="TargetCalories"/>.</summary>
    public required decimal DeviationPct { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierFeedingStatus>))]
public enum ClassifierFeedingStatus
{
    [JsonStringEnumMemberName("EXTREME_UNDER_TARGET")]
    ExtremeUnderTarget,
    [JsonStringEnumMemberName("UNDER_TARGET")]
    UnderTarget,
    [JsonStringEnumMemberName("ON_TARGET")]
    OnTarget,
    [JsonStringEnumMemberName("OVER_TARGET")]
    OverTarget,
    [JsonStringEnumMemberName("EXTREME_OVER_TARGET")]
    ExtremeOverTarget
}
