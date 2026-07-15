using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Domain;

namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record ChatSessionDetailsResponseDto
{
    public required Guid SessionId { get; init; }
    public required Guid PetId { get; init; }
    public required PetType PetType { get; init; }
    public string? SymptomSummary { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public IReadOnlyList<ChatMessageResponseDto> Messages { get; init; } = [];

    public static ChatSessionDetailsResponseDto FromResult(
        ChatSessionDetailsResult result)
    {
        return new ChatSessionDetailsResponseDto
        {
            SessionId = result.SessionId,
            PetId = result.PetId,
            PetType = result.PetType,
            SymptomSummary = result.SymptomSummary,
            CreatedAt = result.CreatedAt,
            UpdatedAt = result.UpdatedAt,
            Messages = result.Messages
                .Select(ChatMessageResponseDto.FromResult)
                .ToList()
        };
    }
}

public sealed record ChatMessageResponseDto
{
    public required Guid MessageId { get; init; }
    public required ChatMessageRole Role { get; init; }
    public ChatMessageStatus? Status { get; init; }
    public required string Content { get; init; }
    public required DateTime CreatedAt { get; init; }

    public static ChatMessageResponseDto FromResult(ChatMessageResult result)
    {
        return new ChatMessageResponseDto
        {
            MessageId = result.MessageId,
            Role = result.Role,
            Status = result.Status,
            Content = result.Content,
            CreatedAt = result.CreatedAt
        };
    }
}
