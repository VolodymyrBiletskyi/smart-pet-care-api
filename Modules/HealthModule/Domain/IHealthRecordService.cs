using smart_pet_care_api.Modules.HealthModule.DTOs.Requests;
using smart_pet_care_api.Modules.HealthModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.HealthModule.Domain
{
    public interface IHealthRecordService
    {
        Task<IReadOnlyList<HealthRecordResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, HealthRecordType? type, SymptomType? symptom, DateTime? from, DateTime? to);
        Task<HealthRecordResponseDto?> GetByIdAsync(Guid petId, Guid recordId, Guid userId);
        Task<HealthRecordResponseDto> CreateAsync(Guid petId, Guid userId, CreateHealthRecordDto dto);
        Task<HealthRecordResponseDto> UpdateAsync(Guid petId, Guid recordId, Guid userId, PatchHealthRecordDto dto);
        Task<bool> DeleteAsync(Guid petId, Guid recordId, Guid userId);
    }
}
