using smart_pet_care_api.Modules.PetModule.DTOs;

namespace smart_pet_care_api.Modules.PetModule.Domain
{
    public interface IPetService
    {
        Task<IReadOnlyList<PetResponseDto>> GetByUserIdAsync(Guid userId);
        Task<PetResponseDto?> GetByIdAsync(Guid id, Guid userId);
        Task<PetResponseDto> CreateAsync(CreatePetDto dto, Guid userId);
        Task<PetResponseDto> UpdateAsync(Guid id, Guid userId, UpdatePetDto dto);
        Task<bool> DeleteAsync(Guid id, Guid userId);
    }
}
