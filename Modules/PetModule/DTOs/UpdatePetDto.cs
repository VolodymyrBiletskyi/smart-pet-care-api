using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.PetModule.DTOs;

public class UpdatePetDto
{
    public string? Name { get; set; }
    public string? Species { get; set; }
    public string? Breed { get; set; }

    public DateTime? BirthDate { get; set; }
    public decimal? WeightKg { get; set; }
    public Sex? Sex { get; set; }

    public string? PhotoUrl { get; set; }
    public string? Allergies { get; set; }
    public string? ChronicConditions { get; set; }
    public string? BehavioralNotes { get; set; }
}