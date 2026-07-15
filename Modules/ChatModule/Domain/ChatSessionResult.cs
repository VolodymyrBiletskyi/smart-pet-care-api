using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.ChatModule.Domain;

public sealed record ChatSessionResult(
    Guid SessionId,
    Guid PetId,
    PetType PetType,
    string? SymptomSummary,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ChatSessionResult FromSession(ChatSession session)
    {
        return new ChatSessionResult(
            session.Id,
            session.PetId,
            session.PetType,
            session.SymptomSummary,
            session.CreatedAt,
            session.UpdatedAt);
    }
}

public sealed record ChatSessionDetailsResult(
    Guid SessionId,
    Guid PetId,
    PetType PetType,
    string? SymptomSummary,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ChatMessageResult> Messages)
{
    public static ChatSessionDetailsResult FromSession(ChatSession session)
    {
        return new ChatSessionDetailsResult(
            session.Id,
            session.PetId,
            session.PetType,
            session.SymptomSummary,
            session.CreatedAt,
            session.UpdatedAt,
            session.Messages
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Select(ChatMessageResult.FromMessage)
                .ToList());
    }
}

public sealed record ChatMessageResult(
    Guid MessageId,
    ChatMessageRole Role,
    string Content,
    DateTime CreatedAt)
{
    public static ChatMessageResult FromMessage(ChatMessage message)
    {
        return new ChatMessageResult(
            message.Id,
            message.Role,
            message.Content,
            message.CreatedAt);
    }
}

public sealed record ChatMessagePageResult(
    Guid SessionId,
    IReadOnlyList<ChatMessageResult> Items,
    int Limit,
    bool HasMore,
    string? NextCursor);
