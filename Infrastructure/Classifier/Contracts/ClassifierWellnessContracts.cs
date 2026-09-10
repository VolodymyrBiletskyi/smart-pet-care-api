using System.Text.Json.Serialization;

namespace smart_pet_care_api.Infrastructure.Classifier.Contracts;

public sealed record ClassifierWellnessRequest
{
    public required ClassifierWellnessPet Pet { get; init; }
    public ClassifierWellnessActivity? Activity { get; init; }
    public ClassifierWellnessFeeding? Feeding { get; init; }
    public IReadOnlyList<ClassifierWellnessCondition> ActiveConditions { get; init; } = [];
    public IReadOnlyList<ClassifierWellnessMedication> ActiveMedications { get; init; } = [];
    public IReadOnlyList<ClassifierWellnessWeightMeasurement> WeightHistory { get; init; } = [];
    public ClassifierWellnessPreventiveCare? PreventiveCare { get; init; }
    public IReadOnlyList<ClassifierWellnessRoutineCareEntry> RoutineCare { get; init; } = [];
    public string? CurrentSymptoms { get; init; }
    public int? PreviousScore { get; init; }
    public ClassifierWellnessEvaluationWindow? EvaluationWindow { get; init; }
}

public sealed record ClassifierWellnessPet
{
    public required string Species { get; init; }
    public string? Breed { get; init; }
    public int? AgeMonths { get; init; }
    public string? Sex { get; init; }
    public decimal? WeightKg { get; init; }
    public string? BehavioralNotes { get; init; }
}

public sealed record ClassifierWellnessActivity
{
    public decimal? AvgStepsPerDay { get; init; }
    public decimal? AvgActiveMinutesPerDay { get; init; }
    public decimal? AvgSleepHoursPerDay { get; init; }
    public int DaysTracked { get; init; }
}

public sealed record ClassifierWellnessFeeding
{
    public decimal? AvgMealsPerDay { get; init; }
    public decimal? AvgCaloriesPerDay { get; init; }
    public IReadOnlyList<string> FoodTypes { get; init; } = [];
    public int ConsistencyDays { get; init; }
}

public sealed record ClassifierWellnessCondition
{
    public required string Name { get; init; }
    public string? TypeLabel { get; init; }
}

public sealed record ClassifierWellnessMedication
{
    public required string Name { get; init; }
    public string? Frequency { get; init; }
    public int? ScheduledDoses { get; init; }
    public int? CompletedDoses { get; init; }
}

public sealed record ClassifierWellnessWeightMeasurement
{
    public required decimal WeightKg { get; init; }
    public required DateTimeOffset MeasuredAt { get; init; }
}

public sealed record ClassifierWellnessPreventiveCare
{
    public bool RecentVetVisit { get; init; }
    public bool VaccinationsUpToDate { get; init; }
}

public sealed record ClassifierWellnessRoutineCareEntry
{
    public required ClassifierWellnessReminderType Type { get; init; }
    public DateOnly? LastDoneAt { get; init; }
}

public sealed record ClassifierWellnessEvaluationWindow
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
}

public sealed record ClassifierWellnessResponse
{
    public int? WellnessScore { get; init; }
    public ClassifierWellnessBand? Band { get; init; }
    public string? BandLabel { get; init; }
    public required ClassifierWellnessScoreStatus ScoreStatus { get; init; }
    public required decimal DataCoverage { get; init; }
    public required string CalculationVersion { get; init; }
    public required DateTimeOffset EvaluatedAt { get; init; }
    public ClassifierWellnessEvaluationWindow? EvaluationWindow { get; init; }
    public ClassifierWellnessTrendDirection? Trend { get; init; }
    public required ClassifierWellnessBreakdown Breakdown { get; init; }
    public int? ConditionCap { get; init; }
    public string? ClassifierCondition { get; init; }
    public required string Narrative { get; init; }
    public required IReadOnlyList<string> Recommendations { get; init; }
    public IReadOnlyList<ClassifierWellnessReminder> Reminders { get; init; } = [];
    public IReadOnlyList<ClassifierWellnessTrackingRecommendation> TrackingRecommendations { get; init; } = [];
    public required string Disclaimer { get; init; }
}

public sealed record ClassifierWellnessBreakdown
{
    public required ClassifierWellnessBreakdownItem Activity { get; init; }
    public required ClassifierWellnessBreakdownItem Sleep { get; init; }
    public required ClassifierWellnessBreakdownItem Diet { get; init; }
    public required ClassifierWellnessBreakdownItem Symptoms { get; init; }
    public required ClassifierWellnessBreakdownItem PreventiveCare { get; init; }
    public required ClassifierWellnessBreakdownItem Baseline { get; init; }

    public IEnumerable<ClassifierWellnessBreakdownItem> Items()
    {
        yield return Activity;
        yield return Sleep;
        yield return Diet;
        yield return Symptoms;
        yield return PreventiveCare;
        yield return Baseline;
    }
}

public sealed record ClassifierWellnessBreakdownItem
{
    public required decimal Score { get; init; }
    public required decimal MaxScore { get; init; }
    public required ClassifierWellnessDimensionAvailability Availability { get; init; }
    public required bool Included { get; init; }
    public required IReadOnlyList<ClassifierWellnessReasonCode> ReasonCodes { get; init; }
    public IReadOnlyDictionary<string, object>? Evidence { get; init; }
}

public sealed record ClassifierWellnessReminder
{
    public required ClassifierWellnessReminderType Reminder { get; init; }
    public required string Text { get; init; }
}

public sealed record ClassifierWellnessTrackingRecommendation
{
    public required ClassifierWellnessDimension Dimension { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<string> RequiredInputs { get; init; }
    public IReadOnlyList<ClassifierWellnessReminderType> SuggestedReminderTypes { get; init; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierWellnessBand>))]
public enum ClassifierWellnessBand
{
    [JsonStringEnumMemberName("EXCELLENT")] Excellent,
    [JsonStringEnumMemberName("GOOD")] Good,
    [JsonStringEnumMemberName("FAIR")] Fair,
    [JsonStringEnumMemberName("CONCERNING")] Concerning,
    [JsonStringEnumMemberName("CRITICAL")] Critical
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierWellnessScoreStatus>))]
public enum ClassifierWellnessScoreStatus
{
    [JsonStringEnumMemberName("COMPLETE")] Complete,
    [JsonStringEnumMemberName("PARTIAL")] Partial,
    [JsonStringEnumMemberName("INSUFFICIENT_DATA")] InsufficientData
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierWellnessTrendDirection>))]
public enum ClassifierWellnessTrendDirection
{
    [JsonStringEnumMemberName("IMPROVING")] Improving,
    [JsonStringEnumMemberName("STABLE")] Stable,
    [JsonStringEnumMemberName("DECLINING")] Declining
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierWellnessDimensionAvailability>))]
public enum ClassifierWellnessDimensionAvailability
{
    [JsonStringEnumMemberName("AVAILABLE")] Available,
    [JsonStringEnumMemberName("MISSING")] Missing,
    [JsonStringEnumMemberName("NOT_APPLICABLE")] NotApplicable
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierWellnessDimension>))]
public enum ClassifierWellnessDimension
{
    Activity,
    Sleep,
    Diet,
    Symptoms,
    PreventiveCare,
    Baseline,
    RoutineCare
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierWellnessReminderType>))]
public enum ClassifierWellnessReminderType
{
    Feeding,
    Activity,
    Medication,
    Vaccination,
    ParasiteTreatment,
    VetVisit,
    Grooming,
    Weighing,
    Deworming,
    Bathing,
    Brushing,
    EarCleaning,
    NailTrimming,
    PawCare,
    TeethCleaning
}

[JsonConverter(typeof(JsonStringEnumConverter<ClassifierWellnessReasonCode>))]
public enum ClassifierWellnessReasonCode
{
    [JsonStringEnumMemberName("ACTIVITY_DATA_MISSING")] ActivityDataMissing,
    [JsonStringEnumMemberName("ACTIVITY_TARGET_MET")] ActivityTargetMet,
    [JsonStringEnumMemberName("ACTIVITY_BELOW_TARGET")] ActivityBelowTarget,
    [JsonStringEnumMemberName("ACTIVITY_NOT_APPLICABLE")] ActivityNotApplicable,
    [JsonStringEnumMemberName("SLEEP_DATA_MISSING")] SleepDataMissing,
    [JsonStringEnumMemberName("SLEEP_WITHIN_RANGE")] SleepWithinRange,
    [JsonStringEnumMemberName("SLEEP_OUTSIDE_RANGE")] SleepOutsideRange,
    [JsonStringEnumMemberName("SLEEP_NOT_APPLICABLE")] SleepNotApplicable,
    [JsonStringEnumMemberName("DIET_DATA_MISSING")] DietDataMissing,
    [JsonStringEnumMemberName("DIET_TRACKING_STRONG")] DietTrackingStrong,
    [JsonStringEnumMemberName("DIET_TRACKING_NEEDS_ATTENTION")] DietTrackingNeedsAttention,
    [JsonStringEnumMemberName("SYMPTOMS_NOT_REPORTED")] SymptomsNotReported,
    [JsonStringEnumMemberName("SYMPTOM_CLASSIFIER_UNAVAILABLE")] SymptomClassifierUnavailable,
    [JsonStringEnumMemberName("SYMPTOM_CLASSIFIER_FAILED")] SymptomClassifierFailed,
    [JsonStringEnumMemberName("SYMPTOM_RESULT_AVAILABLE")] SymptomResultAvailable,
    [JsonStringEnumMemberName("PREVENTIVE_CARE_DATA_MISSING")] PreventiveCareDataMissing,
    [JsonStringEnumMemberName("PREVENTIVE_CARE_CURRENT")] PreventiveCareCurrent,
    [JsonStringEnumMemberName("PREVENTIVE_CARE_NEEDS_ATTENTION")] PreventiveCareNeedsAttention,
    [JsonStringEnumMemberName("BASELINE_DATA_MISSING")] BaselineDataMissing,
    [JsonStringEnumMemberName("BASELINE_STABLE")] BaselineStable,
    [JsonStringEnumMemberName("BASELINE_NEEDS_ATTENTION")] BaselineNeedsAttention
}
