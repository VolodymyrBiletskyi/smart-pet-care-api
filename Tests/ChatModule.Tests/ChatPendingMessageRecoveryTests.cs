using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Recovery;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ChatPendingMessageRecoveryTests
{
    [Fact]
    public async Task RecoverStalePendingMessagesAsync_MarksOnlyExpiredPendingUserMessagesRetryable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateContext(connection);
        await dbContext.Database.EnsureCreatedAsync(
            TestContext.Current.CancellationToken);
        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = "chat-recovery@example.com",
            PasswordHash = "hash"
        };
        var pet = new Pet
        {
            UserId = user.Id,
            Name = "Buddy",
            Species = Enums.AnimalSpecies.Dog
        };
        var session = new ChatSession
        {
            UserId = user.Id,
            PetId = pet.Id,
            PetType = PetType.Dog
        };
        dbContext.AddRange(user, pet, session);
        var stalePending = Message(
            session.Id,
            ChatMessageRole.User,
            ChatMessageStatus.Pending,
            now.AddSeconds(-40));
        var freshPending = Message(
            session.Id,
            ChatMessageRole.User,
            ChatMessageStatus.Pending,
            now.AddSeconds(-20));
        var staleCompleted = Message(
            session.Id,
            ChatMessageRole.User,
            ChatMessageStatus.Completed,
            now.AddSeconds(-40));
        var staleAssistant = Message(
            session.Id,
            ChatMessageRole.Assistant,
            ChatMessageStatus.Pending,
            now.AddSeconds(-40));
        dbContext.ChatMessages.AddRange(
            stalePending,
            freshPending,
            staleCompleted,
            staleAssistant);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var recovery = new ChatPendingMessageRecovery(
            dbContext,
            Options.Create(new ClassifierOptions { TimeoutSeconds = 30 }),
            TimeProvider.System);

        var recovered = await recovery.RecoverStalePendingMessagesAsync(
            TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.ChatMessages.ToDictionaryAsync(
            message => message.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, recovered);
        Assert.Equal(
            ChatMessageStatus.FailedRetryable,
            persisted[stalePending.Id].Status);
        Assert.Equal(ChatMessageStatus.Pending, persisted[freshPending.Id].Status);
        Assert.Equal(ChatMessageStatus.Completed, persisted[staleCompleted.Id].Status);
        Assert.Equal(ChatMessageStatus.Pending, persisted[staleAssistant.Id].Status);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static ChatMessage Message(
        Guid sessionId,
        ChatMessageRole role,
        ChatMessageStatus status,
        DateTime createdAt)
    {
        return new ChatMessage
        {
            SessionId = sessionId,
            Role = role,
            Status = status,
            Content = "message",
            CreatedAt = createdAt
        };
    }
}
