using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.FeedingModule.DTOs.Requests;
using smart_pet_care_api.Modules.FeedingModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.FeedingModule.Mapper
{
    public static class FeedingLogMapper
    {
        public static FeedingLog ToEntity(CreateFeedingLogDto dto, Guid petId) => new()
        {
            PetId = petId,
            FedAt = NormalizeToUtc(dto.FedAt!.Value),
            FoodType = dto.FoodType,
            FoodName = dto.FoodName,
            PortionAmount = dto.PortionAmount,
            PortionUnit = dto.PortionUnit,
            ApproxCalories = dto.ApproxCalories,
            Description = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        public static FeedingLogResponseDto ToDto(this FeedingLog log) => new()
        {
            Id = log.Id,
            PetId = log.PetId,
            FedAt = log.FedAt,
            FoodType = log.FoodType,
            FoodName = log.FoodName,
            PortionAmount = log.PortionAmount,
            PortionUnit = log.PortionUnit,
            ApproxCalories = log.ApproxCalories,
            Notes = log.Description,
            CreatedAt = log.CreatedAt,
            UpdatedAt = log.UpdatedAt
        };

        public static void PatchEntity(this FeedingLog log, PatchFeedingLogDto dto)
        {
            if (dto.FedAt.IsSet) log.FedAt = NormalizeToUtc(dto.FedAt.Value);
            if (dto.FoodType.IsSet) log.FoodType = dto.FoodType.Value;
            if (dto.FoodName.IsSet) log.FoodName = dto.FoodName.Value;
            if (dto.PortionAmount.IsSet) log.PortionAmount = dto.PortionAmount.Value;
            if (dto.PortionUnit.IsSet) log.PortionUnit = dto.PortionUnit.Value;
            if (dto.ApproxCalories.IsSet) log.ApproxCalories = dto.ApproxCalories.Value;
            if (dto.Notes.IsSet) log.Description = dto.Notes.Value;
            log.UpdatedAt = DateTime.UtcNow;
        }

        public static DateTime NormalizeToUtc(DateTime dateTime) =>
            dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
    }
}
