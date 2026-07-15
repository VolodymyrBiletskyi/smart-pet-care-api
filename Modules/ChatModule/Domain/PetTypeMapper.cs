using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.ChatModule.Domain;

public static class PetTypeMapper
{
    public static PetType Map(string species)
    {
        var normalized = species
            .Trim()
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized switch
        {
            "dog" => PetType.Dog,
            "cat" => PetType.Cat,
            "rabbit" => PetType.Rabbit,
            "hamster" => PetType.Hamster,
            "guineapig" => PetType.GuineaPig,
            "bird" => PetType.Bird,
            "fish" => PetType.Fish,
            "turtle" => PetType.Turtle,
            _ => PetType.Other
        };
    }

    public static ClassifierPetType ToClassifierPetType(PetType petType)
    {
        return petType switch
        {
            PetType.Dog => ClassifierPetType.Dog,
            PetType.Cat => ClassifierPetType.Cat,
            PetType.Rabbit => ClassifierPetType.Rabbit,
            PetType.Hamster => ClassifierPetType.Hamster,
            PetType.GuineaPig => ClassifierPetType.GuineaPig,
            PetType.Bird => ClassifierPetType.Bird,
            PetType.Fish => ClassifierPetType.Fish,
            PetType.Turtle => ClassifierPetType.Turtle,
            _ => ClassifierPetType.Other
        };
    }
}
