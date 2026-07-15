using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Domain
{
    public interface IPetWeightLogService
    {
        Task<IReadOnlyList<PetWeightLogResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, DateTime? from = null, DateTime? to = null);
        Task<PetWeightLogResponseDto> CreateAsync(Guid petId, Guid userId, CreatePetWeightLogDto dto);
        Task<PetWeightLogResponseDto> UpdateAsync(Guid petId, Guid weightLogId, Guid userId, PatchPetWeightLogDto dto);
        Task<bool> DeleteAsync(Guid petId, Guid weightLogId, Guid userId);
    }
}
