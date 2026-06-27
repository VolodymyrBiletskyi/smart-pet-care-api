using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetModule.DTOs;

namespace smart_pet_care_api.Modules.PetModule.Mapper;

public static class PetMapper
{
    public static Pet ToEntity(CreatePetDto dto, Guid userId)
    {
        return new Pet
        {
            UserId = userId,

            Name = dto.Name,
            Species = dto.Species,
            Breed = dto.Breed,
            BirthDate = dto.BirthDate,
            WeightKg = dto.WeightKg,
            Sex = dto.Sex,

            Allergies = dto.Allergies,
            ChronicConditions = dto.ChronicConditions,
            BehavioralNotes = dto.BehavioralNotes,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    public static PetResponseDto ToResponseDto(Pet pet)
    {
        return new PetResponseDto
        {
            Id = pet.Id,

            Name = pet.Name,
            Species = pet.Species,
            Breed = pet.Breed,
            BirthDate = pet.BirthDate,
            Age = CalculateAge(pet.BirthDate),
            WeightKg = pet.WeightKg,
            Sex = pet.Sex,

            PhotoUrl = pet.PhotoUrl,
            Allergies = pet.Allergies,
            ChronicConditions = pet.ChronicConditions,
            BehavioralNotes = pet.BehavioralNotes,

            CreatedAt = pet.CreatedAt,
            UpdatedAt = pet.UpdatedAt
        };
    }

    public static void UpdateEntity(Pet pet, UpdatePetDto dto)
    {
        if (dto.Name is not null)
            pet.Name = dto.Name;

        if (dto.Species is not null)
            pet.Species = dto.Species;

        if (dto.Breed is not null)
            pet.Breed = dto.Breed;

        if (dto.BirthDate is not null)
            pet.BirthDate = dto.BirthDate;

        if (dto.WeightKg is not null)
            pet.WeightKg = dto.WeightKg;

        if (dto.Sex is not null)
            pet.Sex = dto.Sex.Value;

        if (dto.Allergies is not null)
            pet.Allergies = dto.Allergies;

        if (dto.ChronicConditions is not null)
            pet.ChronicConditions = dto.ChronicConditions;

        if (dto.BehavioralNotes is not null)
            pet.BehavioralNotes = dto.BehavioralNotes;

        pet.UpdatedAt = DateTime.UtcNow;
    }

    private static int? CalculateAge(DateTime? birthDate)
    {
        if (birthDate is null)
            return null;

        var today = DateTime.UtcNow.Date;
        var birth = birthDate.Value.Date;

        var age = today.Year - birth.Year;

        if (birth > today.AddYears(-age))
            age--;

        return age;
    }
}
