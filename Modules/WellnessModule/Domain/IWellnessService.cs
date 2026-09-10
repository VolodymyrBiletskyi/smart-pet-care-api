using smart_pet_care_api.Modules.WellnessModule.DTOs;

namespace smart_pet_care_api.Modules.WellnessModule.Domain;

public interface IWellnessService
{
    Task<WellnessResponseDto> RecalculateAsync(
        Guid petId, Guid userId, string? currentSymptoms, CancellationToken cancellationToken = default);
    Task<WellnessResponseDto?> GetCurrentAsync(
        Guid petId, Guid userId, CancellationToken cancellationToken = default);
    Task<WellnessHistoryResponseDto> GetHistoryAsync(
        Guid petId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
}
