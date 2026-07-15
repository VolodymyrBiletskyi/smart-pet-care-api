using smart_pet_care_api.Modules.ChatModule.Domain;

namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record SessionMessagesPageResponseDto
{
    public required Guid SessionId { get; init; }
    public IReadOnlyList<ChatMessageResponseDto> Items { get; init; } = [];
    public required MessagePageInfoResponseDto Pagination { get; init; }

    public static SessionMessagesPageResponseDto FromResult(
        ChatMessagePageResult result)
    {
        return new SessionMessagesPageResponseDto
        {
            SessionId = result.SessionId,
            Items = result.Items
                .Select(ChatMessageResponseDto.FromResult)
                .ToList(),
            Pagination = new MessagePageInfoResponseDto
            {
                Limit = result.Limit,
                HasMore = result.HasMore,
                NextCursor = result.NextCursor
            }
        };
    }
}

public sealed record MessagePageInfoResponseDto
{
    public required int Limit { get; init; }
    public required bool HasMore { get; init; }
    public string? NextCursor { get; init; }
}
