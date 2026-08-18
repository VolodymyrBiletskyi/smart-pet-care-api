using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.ChatModule.Domain;

public interface IChatService
{
    Task<IReadOnlyList<ChatSessionResult>> GetSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ChatSessionDetailsResult> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ChatMessagePageResult> GetMessagesAsync(
        Guid sessionId,
        Guid userId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<ChatSessionResult> CreateSessionAsync(
        Guid userId,
        Guid petId,
        CancellationToken cancellationToken = default);

    Task<ClassifierChatResponse> HandleUserMessageAsync(
        Guid sessionId,
        Guid userId,
        string userText,
        Guid clientMessageId,
        CancellationToken cancellationToken = default);

    Task<ClassifierChatResponse> RetryUserMessageAsync(
        Guid sessionId,
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default);
}
