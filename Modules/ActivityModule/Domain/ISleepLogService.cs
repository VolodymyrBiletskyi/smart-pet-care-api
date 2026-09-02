using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.ActivityModule.Domain
{
    public interface ISleepLogService
    {
        Task<IReadOnlyList<SleepLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null);
        Task<SleepLogResponseDto?> GetByIdAsync(Guid petId, Guid sleepLogId, Guid userId);
        Task<SleepLogResponseDto> CreateAsync(Guid petId, Guid userId, CreateSleepLogDto dto);
        Task<bool> DeleteAsync(Guid petId, Guid sleepLogId, Guid userId);
    }
}
