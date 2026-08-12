using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;
using System.Text;
using System.Text.Json;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.ChatModule.Domain;

public sealed class ChatService(
    AppDbContext dbContext,
    IClassifierClient classifierClient) : IChatService
{
    public const int MessageWindowSize = 8;
    public const int DefaultMessagePageSize = 8;
    public const int MaximumMessagePageSize = 8;
    private const int MaximumMessageLength = 4000;

    public async Task<IReadOnlyList<ChatSessionResult>> GetSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.ChatSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.UpdatedAt)
            .ThenByDescending(session => session.Id)
            .ToListAsync(cancellationToken);

        return sessions
            .Select(ChatSessionResult.FromSession)
            .ToList();
    }

    public async Task<ChatSessionDetailsResult> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.ChatSessions
            .AsNoTracking()
            .Include(candidate => candidate.Messages)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == sessionId
                    && candidate.UserId == userId,
                cancellationToken)
            ?? throw new KeyNotFoundException("The chat session was not found.");

        return ChatSessionDetailsResult.FromSession(session);
    }

    public async Task<ChatMessagePageResult> GetMessagesAsync(
        Guid sessionId,
        Guid userId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaximumMessagePageSize)
        {
            throw new ArgumentException(
                $"Limit must be between 1 and {MaximumMessagePageSize}.",
                nameof(limit));
        }

        var sessionExists = await dbContext.ChatSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Id == sessionId
                    && session.UserId == userId,
                cancellationToken);
        if (!sessionExists)
        {
            throw new KeyNotFoundException("The chat session was not found.");
        }

        var position = DecodeCursor(cursor);
        var query = dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId);

        if (position is not null)
        {
            query = query.Where(message =>
                message.CreatedAt < position.CreatedAt
                || (message.CreatedAt == position.CreatedAt
                    && message.Id.CompareTo(position.MessageId) < 0));
        }

        var messages = await query
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = messages.Count > limit;
        if (hasMore)
        {
            messages.RemoveAt(messages.Count - 1);
        }

        var nextCursor = hasMore && messages.Count > 0
            ? EncodeCursor(messages[^1])
            : null;

        messages.Reverse();
        return new ChatMessagePageResult(
            sessionId,
            messages.Select(ChatMessageResult.FromMessage).ToList(),
            limit,
            hasMore,
            nextCursor);
    }

    public async Task<ChatSessionResult> CreateSessionAsync(
        Guid userId,
        Guid petId,
        CancellationToken cancellationToken = default)
    {
        if (petId == Guid.Empty)
        {
            throw new ArgumentException("Pet ID is required.", nameof(petId));
        }

        var species = await dbContext.Pets
            .AsNoTracking()
            .Where(pet => pet.Id == petId && pet.UserId == userId)
            .Select(pet => (Enums.AnimalSpecies?)pet.Species)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The pet was not found.");
        var petType = PetTypeMapper.Map(species);

        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(
                    cancellationToken);
            }

            var existingSession = await dbContext.ChatSessions
                .SingleOrDefaultAsync(
                    session => session.PetId == petId
                        && session.UserId == userId,
                    cancellationToken);

            if (existingSession is not null)
            {
                dbContext.ChatSessions.Remove(existingSession);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var now = DateTime.UtcNow;
            var session = new ChatSession
            {
                UserId = userId,
                PetId = petId,
                PetType = petType,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.ChatSessions.Add(session);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return ChatSessionResult.FromSession(session);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public Task<ClassifierChatResponse> HandleUserMessageAsync(
        Guid sessionId,
        Guid userId,
        string userText,
        CancellationToken cancellationToken = default)
    {
        return HandleUserMessageAsync(
            sessionId,
            userId,
            userText,
            Guid.NewGuid(),
            cancellationToken);
    }

    public Task<ClassifierChatResponse> HandleUserMessageAsync(
        Guid sessionId,
        Guid userId,
        string userText,
        Guid clientMessageId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteSerializedTurnAsync(
            sessionId,
            () => HandleUserMessageCoreAsync(
                sessionId,
                userId,
                userText,
                clientMessageId,
                cancellationToken),
            cancellationToken);
    }

    private async Task<ClassifierChatResponse> HandleUserMessageCoreAsync(
        Guid sessionId,
        Guid userId,
        string userText,
        Guid clientMessageId,
        CancellationToken cancellationToken)
    {
        ValidateUserText(userText);
        if (clientMessageId == Guid.Empty)
        {
            throw new ArgumentException(
                "Client message ID is required.",
                nameof(clientMessageId));
        }

        var session = await dbContext.ChatSessions
            .SingleOrDefaultAsync(
                candidate => candidate.Id == sessionId
                    && candidate.UserId == userId,
                cancellationToken)
            ?? throw new KeyNotFoundException("The chat session was not found.");

        var existingMessage = await dbContext.ChatMessages
            .SingleOrDefaultAsync(
                message => message.SessionId == session.Id
                    && message.ClientMessageId == clientMessageId,
                cancellationToken);
        if (existingMessage is not null)
        {
            return GetIdempotentResponse(existingMessage, userText);
        }

        var userMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = ChatMessageRole.User,
            Status = ChatMessageStatus.Pending,
            ClientMessageId = clientMessageId,
            Content = userText,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ChatMessages.Add(userMessage);
        session.UpdatedAt = userMessage.CreatedAt;

        try
        {
            // Keep the user's source message even when the remote classifier is
            // temporarily unavailable so the session history remains auditable.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have inserted this client message after
            // the lookup above. The unique index is the authoritative guard.
            dbContext.ChangeTracker.Clear();
            var concurrentMessage = await dbContext.ChatMessages
                .SingleOrDefaultAsync(
                    message => message.SessionId == sessionId
                        && message.ClientMessageId == clientMessageId,
                    cancellationToken);
            if (concurrentMessage is not null)
            {
                return GetIdempotentResponse(concurrentMessage, userText);
            }

            throw;
        }

        return await ProcessUserMessageAsync(
            session,
            userMessage,
            cancellationToken);
    }
    public Task<ClassifierChatResponse> RetryUserMessageAsync(
        Guid sessionId,
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteSerializedTurnAsync(
            sessionId,
            () => RetryUserMessageCoreAsync(
                sessionId,
                userId,
                messageId,
                cancellationToken),
            cancellationToken);
    }

    private async Task<ClassifierChatResponse> RetryUserMessageCoreAsync(
        Guid sessionId,
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ChatSessions
            .SingleOrDefaultAsync(
                candidate => candidate.Id == sessionId
                    && candidate.UserId == userId,
                cancellationToken)
            ?? throw new KeyNotFoundException("The chat session was not found.");

        var claimed = await TryClaimFailedRetryableMessageAsync(
            session.Id,
            messageId,
            cancellationToken);
        if (!claimed)
        {
            var messageExists = await dbContext.ChatMessages
                .AsNoTracking()
                .AnyAsync(
                    message => message.Id == messageId
                        && message.SessionId == session.Id
                        && message.Role == ChatMessageRole.User,
                    cancellationToken);
            if (!messageExists)
            {
                throw new KeyNotFoundException("The chat message was not found.");
            }

            throw new InvalidOperationException(
                "Only a failed retryable user message can be retried.");
        }

        var userMessage = await dbContext.ChatMessages
            .SingleAsync(
                message => message.Id == messageId
                    && message.SessionId == session.Id,
                cancellationToken);
        session.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ProcessUserMessageAsync(
            session,
            userMessage,
            cancellationToken);
    }

    private async Task<T> ExecuteSerializedTurnAsync<T>(
        Guid sessionId,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await operation();
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({sessionId.ToString()}, 0));",
                cancellationToken);

            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await transaction.CommitAsync(CancellationToken.None);
            throw;
        }
    }
    private async Task<bool> TryClaimFailedRetryableMessageAsync(
        Guid sessionId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            var message = await dbContext.ChatMessages.SingleOrDefaultAsync(
                candidate => candidate.Id == messageId
                    && candidate.SessionId == sessionId
                    && candidate.Role == ChatMessageRole.User,
                cancellationToken);
            if (message?.Status != ChatMessageStatus.FailedRetryable)
            {
                return false;
            }

            message.Status = ChatMessageStatus.Pending;
            return true;
        }

        var updated = await dbContext.ChatMessages
            .Where(message => message.Id == messageId
                && message.SessionId == sessionId
                && message.Role == ChatMessageRole.User
                && message.Status == ChatMessageStatus.FailedRetryable)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    message => message.Status,
                    ChatMessageStatus.Pending),
                cancellationToken);

        return updated == 1;
    }
    private async Task<ClassifierChatResponse> ProcessUserMessageAsync(
        ChatSession session,
        ChatMessage userMessage,
        CancellationToken cancellationToken)
    {

        var priorMessages = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == session.Id
                && (message.CreatedAt < userMessage.CreatedAt
                    || (message.CreatedAt == userMessage.CreatedAt
                        && message.Id.CompareTo(userMessage.Id) < 0)))
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(MessageWindowSize - 1)
            .Select(message => new ClassifierChatMessage
            {
                Role = message.Role == ChatMessageRole.User
                    ? ClassifierChatRole.User
                    : ClassifierChatRole.Assistant,
                Content = message.Content
            })
            .ToListAsync(cancellationToken);

        priorMessages.Reverse();
        priorMessages.Add(new ClassifierChatMessage
        {
            Role = ClassifierChatRole.User,
            Content = userMessage.Content
        });

        ClassifierChatResponse response;
        try
        {
            response = await classifierClient.ChatAsync(
                new ClassifierChatRequest
                {
                    SessionId = session.Id.ToString("D"),
                    Messages = priorMessages,
                    SymptomSummary = session.SymptomSummary,
                    PetType = PetTypeMapper.ToClassifierPetType(session.PetType)
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            userMessage.Status = ChatMessageStatus.FailedRetryable;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (ClassifierRateLimitedException exception)
        {
            userMessage.Status = ChatMessageStatus.FailedRetryable;
            await dbContext.SaveChangesAsync(cancellationToken);

            throw new ClassifierRateLimitedException(
                exception.Message,
                exception.Code,
                exception.RetryAfterSeconds,
                exception,
                userMessage.Id);
        }
        catch (ClassifierUnavailableException exception)
        {
            userMessage.Status = ChatMessageStatus.FailedRetryable;
            await dbContext.SaveChangesAsync(cancellationToken);

            throw new ClassifierUnavailableException(
                exception.Message,
                exception.StatusCode,
                exception,
                exception.Code,
                exception.RetryAfterSeconds,
                userMessage.Id);
        }

        var assistantMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = ChatMessageRole.Assistant,
            SourceMessageId = userMessage.Id,
            Content = response.Answer,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ChatMessages.Add(assistantMessage);
        userMessage.Status = ChatMessageStatus.Completed;
        userMessage.ClassifierResponseJson = JsonSerializer.Serialize(response);
        session.SymptomSummary = response.SymptomSummary;
        session.UpdatedAt = assistantMessage.CreatedAt;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique SourceMessageId index may have rejected a duplicate
            // assistant reply created by a concurrent or legacy code path.
            dbContext.ChangeTracker.Clear();
            var persistedUserMessage = await dbContext.ChatMessages
                .SingleOrDefaultAsync(
                    message => message.Id == userMessage.Id,
                    cancellationToken);
            if (persistedUserMessage?.Status == ChatMessageStatus.Completed
                && !string.IsNullOrWhiteSpace(
                    persistedUserMessage.ClassifierResponseJson))
            {
                return GetIdempotentResponse(
                    persistedUserMessage,
                    userMessage.Content);
            }

            throw;
        }

        return response;
    }

    private static ClassifierChatResponse GetIdempotentResponse(
        ChatMessage message,
        string userText)
    {
        if (!string.Equals(message.Content, userText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The client message ID has already been used with different text.");
        }

        if (message.Status != ChatMessageStatus.Completed
            || string.IsNullOrWhiteSpace(message.ClassifierResponseJson))
        {
            throw new InvalidOperationException(
                "The client message is already being processed or must be retried.");
        }

        return JsonSerializer.Deserialize<ClassifierChatResponse>(
                   message.ClassifierResponseJson)
               ?? throw new InvalidOperationException(
                   "The stored chat response is invalid.");
    }
    private static void ValidateUserText(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            throw new ArgumentException("Message text is required.", nameof(userText));
        }

        if (userText.Length > MaximumMessageLength)
        {
            throw new ArgumentException(
                $"Message text cannot exceed {MaximumMessageLength} characters.",
                nameof(userText));
        }
    }

    private static string EncodeCursor(ChatMessage message)
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{message.CreatedAt.Ticks}|{message.Id:D}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static MessageCursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var normalized = cursor
                .Replace('-', '+')
                .Replace('_', '/');
            normalized += (normalized.Length % 4) switch
            {
                2 => "==",
                3 => "=",
                0 => string.Empty,
                _ => throw new FormatException()
            };

            var value = Encoding.UTF8.GetString(
                Convert.FromBase64String(normalized));
            var separatorIndex = value.IndexOf('|');
            if (separatorIndex <= 0
                || !long.TryParse(
                    value.AsSpan(0, separatorIndex),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var ticks)
                || !Guid.TryParse(
                    value.AsSpan(separatorIndex + 1),
                    out var messageId))
            {
                throw new FormatException();
            }

            return new MessageCursor(
                new DateTime(ticks, DateTimeKind.Utc),
                messageId);
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException(
                "The message cursor is invalid.",
                nameof(cursor),
                exception);
        }
    }

    private sealed record MessageCursor(DateTime CreatedAt, Guid MessageId);
}
