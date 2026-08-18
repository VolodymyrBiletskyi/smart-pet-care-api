using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Domain;
using Testcontainers.PostgreSql;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ChatSessionConcurrencyPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:16").Build();

    public async ValueTask InitializeAsync()
    {
        await database.StartAsync();
        await using var dbContext = CreateContext();
        await dbContext.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();

    [Fact]
    public async Task ParallelTurns_AreSerializedAndSecondTurnUsesLatestSummary()
    {
        var (sessionId, userId) = await SeedSessionAsync();
        var classifier = new BlockingClassifierClient();

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = new ChatService(firstContext, classifier);
        var secondService = new ChatService(secondContext, classifier);

        var firstTurn = firstService.HandleUserMessageAsync(
            sessionId, userId, "first symptom", Guid.NewGuid());
        await classifier.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var secondTurn = secondService.HandleUserMessageAsync(
            sessionId, userId, "second symptom", Guid.NewGuid());
        await Task.Delay(250);
        Assert.Single(classifier.Requests);

        classifier.CompleteFirst();
        await Task.WhenAll(firstTurn, secondTurn);

        Assert.Equal(2, classifier.Requests.Count);
        var secondRequest = classifier.Requests[1];
        Assert.Equal("summary after first", secondRequest.SymptomSummary);
        Assert.Contains(secondRequest.Messages, message => message.Content == "first symptom");
        Assert.Contains(secondRequest.Messages, message => message.Content == "first answer");
    }

    [Fact]
    public async Task ParallelRequests_WithSameClientMessageId_CreateOneTurnAndReplayResponse()
    {
        var (sessionId, userId) = await SeedSessionAsync();
        var classifier = new BlockingClassifierClient();
        var clientMessageId = Guid.NewGuid();

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = new ChatService(firstContext, classifier);
        var secondService = new ChatService(secondContext, classifier);

        var firstRequest = firstService.HandleUserMessageAsync(
            sessionId, userId, "same symptom", clientMessageId);
        await classifier.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var duplicateRequest = secondService.HandleUserMessageAsync(
            sessionId, userId, "same symptom", clientMessageId);
        await Task.Delay(250);
        Assert.Single(classifier.Requests);

        classifier.CompleteFirst();
        var responses = await Task.WhenAll(firstRequest, duplicateRequest);

        Assert.Equal(responses[0].Answer, responses[1].Answer);
        Assert.Equal(responses[0].SymptomSummary, responses[1].SymptomSummary);
        Assert.Single(classifier.Requests);

        await using var assertionContext = CreateContext();
        Assert.Equal(2, await assertionContext.ChatMessages.CountAsync(
            message => message.SessionId == sessionId));
        Assert.Single(await assertionContext.ChatMessages.Where(
            message => message.SessionId == sessionId
                && message.Role == ChatMessageRole.User).ToListAsync());
        Assert.Single(await assertionContext.ChatMessages.Where(
            message => message.SessionId == sessionId
                && message.Role == ChatMessageRole.Assistant).ToListAsync());
    }

    [Fact]
    public async Task ParallelRetries_OnlyOneProcessesAndCreatesAssistantResponse()
    {
        var (sessionId, userId) = await SeedSessionAsync();
        Guid messageId;
        await using (var seedContext = CreateContext())
        {
            var failedMessage = new ChatMessage
            {
                SessionId = sessionId,
                Role = ChatMessageRole.User,
                Status = ChatMessageStatus.FailedRetryable,
                Content = "retry symptom"
            };
            seedContext.ChatMessages.Add(failedMessage);
            await seedContext.SaveChangesAsync();
            messageId = failedMessage.Id;
        }

        var classifier = new BlockingClassifierClient();
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstService = new ChatService(firstContext, classifier);
        var secondService = new ChatService(secondContext, classifier);

        var firstRetry = firstService.RetryUserMessageAsync(
            sessionId, userId, messageId);
        await classifier.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var duplicateRetry = secondService.RetryUserMessageAsync(
            sessionId, userId, messageId);
        await Task.Delay(250);
        Assert.Single(classifier.Requests);

        classifier.CompleteFirst();
        await firstRetry;
        await Assert.ThrowsAsync<InvalidOperationException>(() => duplicateRetry);

        Assert.Single(classifier.Requests);
        await using var assertionContext = CreateContext();
        var userMessage = await assertionContext.ChatMessages.SingleAsync(
            message => message.Id == messageId);
        Assert.Equal(ChatMessageStatus.Completed, userMessage.Status);
        Assert.Single(await assertionContext.ChatMessages.Where(
            message => message.SourceMessageId == messageId).ToListAsync());
    }

    [Fact]
    public async Task AssistantSourceMessageId_UniqueIndexRejectsSecondResponse()
    {
        var (sessionId, _) = await SeedSessionAsync();
        await using var dbContext = CreateContext();
        var userMessage = new ChatMessage
        {
            SessionId = sessionId,
            Role = ChatMessageRole.User,
            Status = ChatMessageStatus.Completed,
            Content = "source symptom"
        };
        dbContext.ChatMessages.Add(userMessage);
        await dbContext.SaveChangesAsync();

        dbContext.ChatMessages.AddRange(
            new ChatMessage
            {
                SessionId = sessionId,
                Role = ChatMessageRole.Assistant,
                SourceMessageId = userMessage.Id,
                Content = "first response"
            },
            new ChatMessage
            {
                SessionId = sessionId,
                Role = ChatMessageRole.Assistant,
                SourceMessageId = userMessage.Id,
                Content = "duplicate response"
            });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            dbContext.SaveChangesAsync());
    }
    private AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options);
    }

    private async Task<(Guid SessionId, Guid UserId)> SeedSessionAsync()
    {
        await using var dbContext = CreateContext();
        var user = new User { Email = $"chat-concurrency-{Guid.NewGuid():N}@example.com", PasswordHash = "hash" };
        var pet = new Pet { UserId = user.Id, Name = "Buddy", Species = Enums.AnimalSpecies.Dog };
        var session = new ChatSession { UserId = user.Id, PetId = pet.Id, PetType = PetType.Dog };
        dbContext.AddRange(user, pet, session);
        await dbContext.SaveChangesAsync();
        return (session.Id, user.Id);
    }

    private sealed class BlockingClassifierClient : IClassifierClient
    {
        private readonly TaskCompletionSource<ClassifierChatResponse> firstResponse = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstRequestStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ClassifierChatRequest> Requests { get; } = [];

        public Task<ClassifierChatResponse> ChatAsync(
            ClassifierChatRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Requests.Count == 1)
            {
                FirstRequestStarted.TrySetResult();
                return firstResponse.Task.WaitAsync(cancellationToken);
            }

            return Task.FromResult(Response("second answer", "summary after second"));
        }

        public Task<ClassifierFeedingSummaryResponse> SummarizeFeedingAsync(
            ClassifierFeedingSummaryRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void CompleteFirst() => firstResponse.TrySetResult(
            Response("first answer", "summary after first"));

        private static ClassifierChatResponse Response(string answer, string summary) => new()
        {
            Mode = ClassifierChatMode.General,
            Answer = answer,
            SymptomSummary = summary,
            Disclaimer = "Veterinary disclaimer"
        };
    }
}