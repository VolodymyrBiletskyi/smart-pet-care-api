using smart_pet_care_api.Common.Patching;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.PetModule.DTOs;

public class UpdatePetDto
{
    public string? Name { get; set; }
    public AnimalSpecies? Species { get; set; }
    public string? Breed { get; set; }

    public DateTime? BirthDate { get; set; }
    public Sex? Sex { get; set; }

    public PatchField<string?> PhotoUrl { get; set; }
    public PatchField<string?> PhotoPublicId { get; set; }
    public PatchField<List<string>?> Allergies { get; set; }
    public PatchField<List<string>?> ChronicConditions { get; set; }
    public PatchField<List<string>?> BehavioralNotes { get; set; }
}
