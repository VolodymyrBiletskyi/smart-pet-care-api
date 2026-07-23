using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Common.Symptoms
{
    public class SymptomCatalogItemDto
    {
        public int Id { get; set; }
        public SymptomType Name { get; set; }
        public string Label { get; set; } = null!;
        public string Category { get; set; } = null!;
    }
}
