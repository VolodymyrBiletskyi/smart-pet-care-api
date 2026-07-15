using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ChatModule.Domain;

public static class PetTypeMapper
{
    public static PetType Map(AnimalSpecies species)
    {
        return species switch
        {
            AnimalSpecies.Dog => PetType.Dog,
            AnimalSpecies.Cat => PetType.Cat,
            AnimalSpecies.Rabbit => PetType.Rabbit,
            AnimalSpecies.Hamster => PetType.Hamster,
            AnimalSpecies.GuineaPig => PetType.GuineaPig,
            AnimalSpecies.Bird => PetType.Bird,
            AnimalSpecies.Fish => PetType.Fish,
            AnimalSpecies.Turtle => PetType.Turtle,
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
