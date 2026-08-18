using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Mapper
{
    public static class PetWeightLogMapper
    {
        public static PetWeightLog ToEntity(CreatePetWeightLogDto dto, Guid petId) => new()
        {
            PetId = petId,
            ReminderId = dto.ReminderId,
            WeightKg = dto.WeightKg,
            MeasuredAt = NormalizeToUtc(dto.MeasuredAt!.Value),
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        public static PetWeightLogResponseDto ToDto(this PetWeightLog log) => new()
        {
            Id = log.Id,
            PetId = log.PetId,
            ReminderId = log.ReminderId,
            WeightKg = log.WeightKg,
            MeasuredAt = log.MeasuredAt,
            Notes = log.Notes,
            CreatedAt = log.CreatedAt,
            UpdatedAt = log.UpdatedAt
        };

        public static void PatchEntity(this PetWeightLog log, PatchPetWeightLogDto dto)
        {
            var hasChanges = dto.WeightKg.IsSet || dto.MeasuredAt.IsSet || dto.Notes.IsSet;

            if (dto.WeightKg.IsSet) log.WeightKg = dto.WeightKg.Value;
            if (dto.MeasuredAt.IsSet) log.MeasuredAt = NormalizeToUtc(dto.MeasuredAt.Value);
            if (dto.Notes.IsSet) log.Notes = dto.Notes.Value;
            if (hasChanges)
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
