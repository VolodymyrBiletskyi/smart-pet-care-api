using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    public interface IReminderService
    {
        Task<IReadOnlyList<ReminderResponseDto>> GetByUserIdAsync(Guid userId);
        Task<IReadOnlyList<ReminderResponseDto>> GetByPetIdAsync(Guid petId, Guid userId);
        Task<ReminderResponseDto?> GetByIdAsync(Guid id, Guid userId);
        Task<ReminderResponseDto> CreateAsync(CreateReminderDto dto, Guid userId);
        Task<ReminderResponseDto> UpdateAsync(Guid id, PatchReminderDto dto, Guid userId);
        Task DeleteAsync(Guid id, Guid userId);
        Task<IReadOnlyList<ReminderRunResponseDto>> GetRunsAsync(Guid reminderId, Guid userId);
        Task<ReminderRunResponseDto> AcknowledgeRunAsync(Guid runId, Guid userId);

        /// <summary>Pet-wide execution history over a period, for analysis rather than one rule.</summary>
        Task<IReadOnlyList<ReminderRunHistoryDto>> GetRunHistoryAsync(
            Guid petId, Guid userId, DateTime? from, DateTime? to, ReminderType? type);

        /// <summary>Stored runs merged with occurrences projected from the repeat rules.</summary>
        Task<IReadOnlyList<ReminderOccurrenceDto>> GetOccurrencesAsync(
            Guid userId, Guid? petId, DateTime from, DateTime to);
    }
}
