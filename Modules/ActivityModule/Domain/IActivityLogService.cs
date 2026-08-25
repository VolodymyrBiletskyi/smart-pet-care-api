using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Domain
{
    public interface IActivityLogService
    {
        Task<IReadOnlyList<ActivityLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null, ActivitySource? source = null);
        Task<ActivityLogResponseDto?> GetByIdAsync(Guid petId, Guid activityLogId, Guid userId);
        Task<ActivityLogResponseDto> CreateAsync(Guid petId, Guid userId, CreateActivityLogDto dto);
        Task<bool> DeleteAsync(Guid petId, Guid activityLogId, Guid userId);
    }
}
