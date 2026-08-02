using System.Text.Json.Serialization;

namespace smart_pet_care_api.Infrastructure.Classifier.Contracts;

public sealed record ClassifierNutritionRequest
{
    public string? RequestId { get; init; }
    public ClassifierPetType? PetType { get; init; }
    public string? Breed { get; init; }
    public decimal? WeightKg { get; init; }
    public int? AgeMonths { get; init; }

    /// <summary>The analysed local day, as <c>yyyy-MM-dd</c>.</summary>
    public required string Date { get; init; }

    public required int MealCount { get; init; }
    public required int TotalCalories { get; init; }
    public IReadOnlyList<ClassifierNutritionPortionTotal> PortionTotals { get; init; } = [];
    public required int ScheduledFeedings { get; init; }

    public ClassifierNutritionGoal? Goal { get; init; }
    public ClassifierNutritionComparison? Comparison { get; init; }
}

public sealed record ClassifierNutritionPortionTotal
{
    public required ClassifierPortionUnit Unit { get; init; }
    public required decimal TotalAmount { get; init; }
}

public sealed record ClassifierNutritionGoal
{
    public int? DailyCalorieTarget { get; init; }
    public decimal? DailyPortionTarget { get; init; }
    public ClassifierPortionUnit? PortionUnit { get; init; }
    public int? MealsPerDay { get; init; }
}

public sealed record ClassifierNutritionComparison
{
    public int? CaloriesRemaining { get; init; }
    public bool? CalorieTargetMet { get; init; }
    public int? MealsRemaining { get; init; }
    public bool? MealsTargetMet { get; init; }
    public decimal? PortionRemaining { get; init; }
    public bool? PortionTargetMet { get; init; }
}

public sealed record ClassifierNutritionResponse
{
    public required ClassifierNutritionGrade Grade { get; init; }

    /// <summary>Overall quality of the day, from 0 to 100.</summary>
    public required int Score { get; init; }

    public required string Summary { get; init; }
    public IReadOnlyList<string> Advice { get; init; } = [];
    public required string Disclaimer { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierNutritionGrade>))]
public enum ClassifierNutritionGrade
{
    [JsonStringEnumMemberName("A")]
    A,
    [JsonStringEnumMemberName("B")]
    B,
    [JsonStringEnumMemberName("C")]
    C,
    [JsonStringEnumMemberName("D")]
    D,
    [JsonStringEnumMemberName("F")]
    F
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierPortionUnit>))]
public enum ClassifierPortionUnit
{
    [JsonStringEnumMemberName("gram")]
    Gram,
    [JsonStringEnumMemberName("milliliter")]
    Milliliter,
    [JsonStringEnumMemberName("cup")]
    Cup,
    [JsonStringEnumMemberName("piece")]
    Piece
}
