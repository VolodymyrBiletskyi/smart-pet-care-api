using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Common.Symptoms
{
    /// <summary>
    /// Read-only reference catalog of the symptoms a user can attach to a
    /// journal entry or health record. Backed by the <see cref="SymptomType"/>
    /// enum, so each item's Id is the enum's integer value.
    /// </summary>
    public static class SymptomCatalog
    {
        private static readonly IReadOnlyList<SymptomCatalogItemDto> _items = new[]
        {
            Item(SymptomType.Fever, "Fever (high temperature)", "General"),
            Item(SymptomType.Lethargy, "Lethargy / low energy", "General"),
            Item(SymptomType.WeightLoss, "Weight loss", "General"),
            Item(SymptomType.WeightGain, "Weight gain", "General"),
            Item(SymptomType.Dehydration, "Dehydration", "General"),
            Item(SymptomType.Pain, "Pain / discomfort", "General"),

            Item(SymptomType.Vomiting, "Vomiting", "Digestive"),
            Item(SymptomType.Diarrhea, "Diarrhea", "Digestive"),
            Item(SymptomType.Constipation, "Constipation", "Digestive"),
            Item(SymptomType.LossOfAppetite, "Loss of appetite", "Digestive"),
            Item(SymptomType.IncreasedAppetite, "Increased appetite", "Digestive"),

            Item(SymptomType.Coughing, "Coughing", "Respiratory"),
            Item(SymptomType.Sneezing, "Sneezing", "Respiratory"),
            Item(SymptomType.NasalDischarge, "Nasal discharge", "Respiratory"),
            Item(SymptomType.DifficultyBreathing, "Difficulty breathing", "Respiratory"),

            Item(SymptomType.Itching, "Itching / scratching", "Skin & coat"),
            Item(SymptomType.HairLoss, "Hair loss", "Skin & coat"),
            Item(SymptomType.Swelling, "Swelling", "Skin & coat"),

            Item(SymptomType.IncreasedThirst, "Increased thirst", "Urinary"),
            Item(SymptomType.FrequentUrination, "Frequent urination", "Urinary"),

            Item(SymptomType.EyeDischarge, "Eye discharge", "Eyes & ears"),
            Item(SymptomType.EarDischarge, "Ear discharge", "Eyes & ears"),

            Item(SymptomType.Seizure, "Seizure", "Neurological & mobility"),
            Item(SymptomType.Limping, "Limping", "Neurological & mobility"),

            Item(SymptomType.Bleeding, "Bleeding", "Other"),
            Item(SymptomType.Other, "Other", "Other"),
        };

        public static IReadOnlyList<SymptomCatalogItemDto> Items => _items;

        public static SymptomCatalogItemDto? GetById(int id) =>
            _items.FirstOrDefault(i => i.Id == id);

        private static SymptomCatalogItemDto Item(SymptomType type, string label, string category) => new()
        {
            Id = (int)type,
            Name = type,
            Label = label,
            Category = category
        };
    }
}
