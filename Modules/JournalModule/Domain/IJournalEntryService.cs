using smart_pet_care_api.Modules.JournalModule.DTOs.Requests;
using smart_pet_care_api.Modules.JournalModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.JournalModule.Domain
{
    public interface IJournalEntryService
    {
        Task<IReadOnlyList<JournalEntryResponseDto>> GetByPetIdAsync(Guid petId, Guid userId, JournalEntryType? type, JournalEntrySeverity? severity, SymptomType? symptom, DateTime? from, DateTime? to);
        Task<JournalEntryResponseDto?> GetByIdAsync(Guid petId, Guid entryId, Guid userId);
        Task<JournalEntryResponseDto> CreateAsync(Guid petId, Guid userId, CreateJournalEntryDto dto);
        Task<JournalEntryResponseDto> UpdateAsync(Guid petId, Guid entryId, Guid userId, PatchJournalEntryDto dto);
        Task<bool> DeleteAsync(Guid petId, Guid entryId, Guid userId);
    }
}
