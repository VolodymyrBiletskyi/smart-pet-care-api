using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Domain;

namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record ChatSessionResponseDto
{
    public required Guid SessionId { get; init; }
    public required Guid PetId { get; init; }
    public required PetType PetType { get; init; }
    public string? SymptomSummary { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    public static ChatSessionResponseDto FromResult(ChatSessionResult result)
    {
        return new ChatSessionResponseDto
        {
            SessionId = result.SessionId,
            PetId = result.PetId,
            PetType = result.PetType,
            SymptomSummary = result.SymptomSummary,
            CreatedAt = result.CreatedAt,
            UpdatedAt = result.UpdatedAt
        };
    }
}
