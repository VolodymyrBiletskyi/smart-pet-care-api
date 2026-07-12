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

            Name = NormalizeRequiredText(dto.Name),
            Species = NormalizeRequiredText(dto.Species),
            Breed = NormalizeOptionalText(dto.Breed),
            BirthDate = dto.BirthDate,
            WeightKg = dto.WeightKg,
            Sex = dto.Sex,

            PhotoUrl = dto.PhotoUrl,
            PhotoPublicId = dto.PhotoPublicId,
            Allergies = NormalizeOptionalTextItems(dto.Allergies),
            ChronicConditions = NormalizeOptionalTextItems(dto.ChronicConditions),
            BehavioralNotes = NormalizeOptionalTextItems(dto.BehavioralNotes),

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
            PhotoPublicId = pet.PhotoPublicId,
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
            pet.Name = NormalizeRequiredText(dto.Name);

        if (dto.Species is not null)
            pet.Species = NormalizeRequiredText(dto.Species);

        if (dto.Breed is not null)
            pet.Breed = NormalizeOptionalText(dto.Breed);

        if (dto.BirthDate is not null)
            pet.BirthDate = dto.BirthDate;

        if (dto.Sex is not null)
            pet.Sex = dto.Sex.Value;

        if (dto.PhotoUrl.IsSet)
            pet.PhotoUrl = dto.PhotoUrl.Value;

        if (dto.PhotoPublicId.IsSet)
            pet.PhotoPublicId = dto.PhotoPublicId.Value;

        if (dto.Allergies.IsSet)
            pet.Allergies = NormalizeOptionalTextItems(dto.Allergies.Value);

        if (dto.ChronicConditions.IsSet)
            pet.ChronicConditions = NormalizeOptionalTextItems(dto.ChronicConditions.Value);

        if (dto.BehavioralNotes.IsSet)
            pet.BehavioralNotes = NormalizeOptionalTextItems(dto.BehavioralNotes.Value);

        pet.UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeRequiredText(string value) => value.Trim();

    private static string? NormalizeOptionalText(string? value)
    {
        if (value is null)
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static List<string>? NormalizeOptionalTextItems(IReadOnlyCollection<string>? values)
    {
        if (values is null)
            return null;

        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();

        return normalized.Count == 0 ? null : normalized;
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
