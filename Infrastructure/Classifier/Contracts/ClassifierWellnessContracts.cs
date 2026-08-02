namespace smart_pet_care_api.Infrastructure.Classifier.Contracts;

/// <summary>
/// The subset of the classifier's <c>wellness</c> contract the nutrition
/// analysis uses. Wellness scores a pet across six dimensions; nutrition only
/// supplies and reads the <c>diet</c> one, so the other dimensions are left
/// unset and their part of the response is ignored.
/// </summary>
public sealed record ClassifierWellnessRequest
{
    public required ClassifierWellnessPet Pet { get; init; }
    public ClassifierWellnessFeeding? Feeding { get; init; }
    public ClassifierWellnessEvaluationWindow? EvaluationWindow { get; init; }
}

public sealed record ClassifierWellnessPet
{
    /// <summary>Wire values match <see cref="ClassifierPetType"/>.</summary>
    public required ClassifierPetType Species { get; init; }

    public string? Breed { get; init; }
    public int? AgeMonths { get; init; }
    public decimal? WeightKg { get; init; }
}

public sealed record ClassifierWellnessFeeding
{
    public double? AvgMealsPerDay { get; init; }
    public double? AvgCaloriesPerDay { get; init; }
    public IReadOnlyList<string> FoodTypes { get; init; } = [];

    /// <summary>
    /// Days of feeding history behind the averages. The classifier caps this at
    /// <see cref="MaximumConsistencyDays"/> and rejects anything higher.
    /// </summary>
    public required int ConsistencyDays { get; init; }

    public const int MaximumConsistencyDays = 7;
}

public sealed record ClassifierWellnessEvaluationWindow
{
    /// <summary>Inclusive start of the window, as <c>yyyy-MM-dd</c>.</summary>
    public required string StartDate { get; init; }

    /// <summary>Inclusive end of the window, as <c>yyyy-MM-dd</c>.</summary>
    public required string EndDate { get; init; }
}

public sealed record ClassifierWellnessResponse
{
    public required ClassifierWellnessBreakdown Breakdown { get; init; }
    public string? Disclaimer { get; init; }
}

public sealed record ClassifierWellnessBreakdown
{
    public required ClassifierWellnessBreakdownItem Diet { get; init; }
}

public sealed record ClassifierWellnessBreakdownItem
{
    public required double Score { get; init; }
    public required double MaxScore { get; init; }

    /// <summary>
    /// <c>AVAILABLE</c> when the dimension was scored. Left as a string because
    /// the classifier may add availability states within contract v1.
    /// </summary>
    public string? Availability { get; init; }

    public bool Included { get; init; }
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public ClassifierWellnessDietEvidence? Evidence { get; init; }
}

/// <summary>
/// The figures the classifier derived for the diet dimension. The nutrition
/// analysis grades on <see cref="CalorieRatio"/> rather than the dimension
/// score, because the score also rewards how many days were logged and a
/// single-day analysis can never earn that part.
/// </summary>
public sealed record ClassifierWellnessDietEvidence
{
    /// <summary>Daily calorie target the classifier derived from the pet's body data.</summary>
    public double? CalorieTargetPerDay { get; init; }

    /// <summary>Logged calories divided by <see cref="CalorieTargetPerDay"/>. 1.0 is on target.</summary>
    public double? CalorieRatio { get; init; }

    public double? AvgMealsPerDay { get; init; }
    public double? AvgCaloriesPerDay { get; init; }
}
