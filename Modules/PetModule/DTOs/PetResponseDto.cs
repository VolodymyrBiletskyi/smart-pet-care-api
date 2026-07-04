using static smart_pet_care_api.Models.Enums;
namespace smart_pet_care_api.Modules.PetModule.DTOs;

public class PetResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Species { get; set; } = null!;
    public string? Breed { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }
    public decimal? WeightKg { get; set; }
    public Sex Sex { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PhotoPublicId { get; set; }
    public List<string>? Allergies { get; set; }
    public List<string>? ChronicConditions { get; set; }
    public List<string>? BehavioralNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
