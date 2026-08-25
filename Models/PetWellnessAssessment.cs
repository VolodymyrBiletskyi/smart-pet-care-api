namespace smart_pet_care_api.Models;

public sealed class PetWellnessAssessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PetId { get; set; }
    public int? WellnessScore { get; set; }
    public string? Band { get; set; }
    public string ScoreStatus { get; set; } = null!;
    public decimal DataCoverage { get; set; }
    public string CalculationVersion { get; set; } = null!;
    public DateTime EvaluatedAt { get; set; }
    public DateOnly? WindowStartDate { get; set; }
    public DateOnly? WindowEndDate { get; set; }
    public string? Trend { get; set; }
    public string ResponseJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Pet? Pet { get; set; }
}
