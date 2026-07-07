using smart_pet_care_api.Modules.FeedingModule.DTOs.Requests;
using smart_pet_care_api.Modules.FeedingModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.FeedingModule.Domain
{
    public interface IFeedingLogService
    {
        Task<IReadOnlyList<FeedingLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId);
        Task<FeedingLogResponseDto?> GetByIdAsync(Guid petId, Guid logId, Guid userId);
        Task<FeedingLogResponseDto> CreateAsync(Guid petId, Guid userId, CreateFeedingLogDto dto);
        Task<FeedingLogResponseDto> UpdateAsync(Guid petId, Guid logId, Guid userId, PatchFeedingLogDto dto);
        Task<bool> DeleteAsync(Guid petId, Guid logId, Guid userId);
    }
}
