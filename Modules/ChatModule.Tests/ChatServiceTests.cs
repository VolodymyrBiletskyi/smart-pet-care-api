using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Domain;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task HandleUserMessageAsync_WhileClassifierIsRunning_KeepsUserPending()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "initial summary");
        var client = new ControlledClassifierClient();
        var service = new ChatService(dbContext, client);

        var call = service.HandleUserMessageAsync(
            session.Id,
            session.UserId,
            "new symptom",
            TestContext.Current.CancellationToken);
        await client.RequestStarted.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var message = await dbContext.ChatMessages.SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(ChatMessageStatus.Pending, message.Status);

        client.Complete(CreateResponse("answer", "updated summary"));
        await call;

        Assert.Equal(ChatMessageStatus.Completed, message.Status);
    }

    [Fact]
    public async Task HandleUserMessageAsync_AppendsAssistantAndOverwritesSummaryEveryTurn()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "initial summary");
        var client = new QueueClassifierClient(
            CreateResponse("first answer", "first summary"),
            CreateResponse("second answer", "second summary"));
        var service = new ChatService(dbContext, client);

        await service.HandleUserMessageAsync(
            session.Id,
            session.UserId,
            "first question",
            TestContext.Current.CancellationToken);
        await service.HandleUserMessageAsync(
            session.Id,
            session.UserId,
            "second question",
            TestContext.Current.CancellationToken);

        var messages = await dbContext.ChatMessages
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);
        var persistedSession = await dbContext.ChatSessions.SingleAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(4, messages.Count);
        Assert.Equal(
            [
                ChatMessageRole.User,
                ChatMessageRole.Assistant,
                ChatMessageRole.User,
                ChatMessageRole.Assistant
            ],
            messages.Select(message => message.Role));
        Assert.Equal("first answer", messages[1].Content);
        Assert.Equal("second answer", messages[3].Content);
        Assert.Equal(ChatMessageStatus.Completed, messages[0].Status);
        Assert.Null(messages[1].Status);
        Assert.Equal(ChatMessageStatus.Completed, messages[2].Status);
        Assert.Null(messages[3].Status);
        Assert.Equal("second summary", persistedSession.SymptomSummary);
    }

    [Fact]
    public async Task HandleUserMessageAsync_WhenClassifierIsUnavailable_KeepsUserAndSummary()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "last good summary");
        var service = new ChatService(
            dbContext,
            new ThrowingClassifierClient());

        var exception = await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            service.HandleUserMessageAsync(
                session.Id,
                session.UserId,
                "new symptom",
                TestContext.Current.CancellationToken));

        var message = await dbContext.ChatMessages.SingleAsync(
            TestContext.Current.CancellationToken);
        var persistedSession = await dbContext.ChatSessions.SingleAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ChatMessageRole.User, message.Role);
        Assert.Equal("new symptom", message.Content);
        Assert.Equal(ChatMessageStatus.FailedRetryable, message.Status);
        Assert.Equal(message.Id, exception.MessageId);
        Assert.Equal("last good summary", persistedSession.SymptomSummary);
    }

    [Fact]
    public async Task HandleUserMessageAsync_WhenClassifierIsRateLimited_MarksUserRetryableAndReturnsMessageId()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "last good summary");
        var service = new ChatService(
            dbContext,
            new ThrowingClassifierClient(
                new ClassifierRateLimitedException(
                    "Classifier rate limit exceeded.",
                    "rate_limit_exceeded",
                    retryAfterSeconds: 30)));

        var exception = await Assert.ThrowsAsync<ClassifierRateLimitedException>(() =>
            service.HandleUserMessageAsync(
                session.Id,
                session.UserId,
                "new symptom",
                TestContext.Current.CancellationToken));

        var message = await dbContext.ChatMessages.SingleAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(ChatMessageStatus.FailedRetryable, message.Status);
        Assert.Equal(message.Id, exception.MessageId);
        Assert.Equal(30, exception.RetryAfterSeconds);
    }

    [Fact]
    public async Task RetryUserMessageAsync_ReusesFailedMessageWithoutCreatingUserDuplicate()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "last good summary");
        var failedMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = ChatMessageRole.User,
            Status = ChatMessageStatus.FailedRetryable,
            Content = "new symptom",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        dbContext.ChatMessages.Add(failedMessage);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var client = new QueueClassifierClient(
            CreateResponse("answer", "updated summary"));
        var service = new ChatService(dbContext, client);

        var response = await service.RetryUserMessageAsync(
            session.Id,
            session.UserId,
            failedMessage.Id,
            TestContext.Current.CancellationToken);

        var messages = await dbContext.ChatMessages
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);
        var persistedUserMessage = Assert.Single(
            messages,
            message => message.Role == ChatMessageRole.User);
        var assistantMessage = Assert.Single(
            messages,
            message => message.Role == ChatMessageRole.Assistant);

        Assert.Equal("answer", response.Answer);
        Assert.Equal(failedMessage.Id, persistedUserMessage.Id);
        Assert.Equal(ChatMessageStatus.Completed, persistedUserMessage.Status);
        Assert.Equal("answer", assistantMessage.Content);
        Assert.Equal("new symptom", Assert.Single(client.Requests).Messages[^1].Content);
    }

    [Fact]
    public async Task RetryUserMessageAsync_WhenClassifierStillUnavailable_KeepsSameFailedMessage()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "last good summary");
        var failedMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = ChatMessageRole.User,
            Status = ChatMessageStatus.FailedRetryable,
            Content = "new symptom"
        };
        dbContext.ChatMessages.Add(failedMessage);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ChatService(dbContext, new ThrowingClassifierClient());

        var exception = await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            service.RetryUserMessageAsync(
                session.Id,
                session.UserId,
                failedMessage.Id,
                TestContext.Current.CancellationToken));

        var persistedMessage = await dbContext.ChatMessages.SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(failedMessage.Id, persistedMessage.Id);
        Assert.Equal(ChatMessageStatus.FailedRetryable, persistedMessage.Status);
        Assert.Equal(failedMessage.Id, exception.MessageId);
    }

    [Fact]
    public async Task RetryUserMessageAsync_WhenMessageIsCompleted_RejectsRetry()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "summary");
        var completedMessage = new ChatMessage
        {
            SessionId = session.Id,
            Role = ChatMessageRole.User,
            Status = ChatMessageStatus.Completed,
            Content = "completed symptom"
        };
        dbContext.ChatMessages.Add(completedMessage);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ChatService(
            dbContext,
            new QueueClassifierClient(CreateResponse("unused", "unused")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RetryUserMessageAsync(
                session.Id,
                session.UserId,
                completedMessage.Id,
                TestContext.Current.CancellationToken));

        Assert.Single(await dbContext.ChatMessages.ToListAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HandleUserMessageAsync_SendsLastEightMessagesAndSessionContext()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "rolling summary from older turns");
        var start = DateTime.UtcNow.AddHours(-1);

        for (var index = 0; index < 12; index++)
        {
            dbContext.ChatMessages.Add(new ChatMessage
            {
                SessionId = session.Id,
                Role = index % 2 == 0
                    ? ChatMessageRole.User
                    : ChatMessageRole.Assistant,
                Content = $"message-{index}",
                CreatedAt = start.AddMinutes(index)
            });
        }

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var client = new QueueClassifierClient(
            CreateResponse("answer", "updated summary"));
        var service = new ChatService(dbContext, client);

        await service.HandleUserMessageAsync(
            session.Id,
            session.UserId,
            "current message",
            TestContext.Current.CancellationToken);

        var request = Assert.Single(client.Requests);
        Assert.Equal(ChatService.MessageWindowSize, request.Messages.Count);
        Assert.Equal("message-5", request.Messages[0].Content);
        Assert.Equal("current message", request.Messages[^1].Content);
        Assert.Equal(ClassifierChatRole.User, request.Messages[^1].Role);
        Assert.Equal("rolling summary from older turns", request.SymptomSummary);
        Assert.Equal(session.Id.ToString("D"), request.SessionId);
        Assert.Equal(ClassifierPetType.GuineaPig, request.PetType);
    }

    [Fact]
    public async Task HandleUserMessageAsync_ForAnotherUsersSession_ReturnsNotFound()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "summary");
        var service = new ChatService(
            dbContext,
            new QueueClassifierClient(CreateResponse("answer", "summary")));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.HandleUserMessageAsync(
                session.Id,
                Guid.NewGuid(),
                "message",
                TestContext.Current.CancellationToken));

        Assert.Empty(await dbContext.ChatMessages.ToListAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsEightThenOlderPageWithoutDuplicates()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "summary");
        var start = DateTime.UtcNow.AddHours(-1);
        for (var index = 0; index < 12; index++)
        {
            dbContext.ChatMessages.Add(new ChatMessage
            {
                SessionId = session.Id,
                Role = index % 2 == 0
                    ? ChatMessageRole.User
                    : ChatMessageRole.Assistant,
                Content = $"message-{index}",
                CreatedAt = start.AddMinutes(index)
            });
        }
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ChatService(
            dbContext,
            new QueueClassifierClient(CreateResponse("unused", "unused")));

        var first = await service.GetMessagesAsync(
            session.Id,
            session.UserId,
            limit: 8,
            cursor: null,
            TestContext.Current.CancellationToken);
        var second = await service.GetMessagesAsync(
            session.Id,
            session.UserId,
            limit: 8,
            first.NextCursor,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            Enumerable.Range(4, 8).Select(index => $"message-{index}"),
            first.Items.Select(message => message.Content));
        Assert.True(first.HasMore);
        Assert.NotNull(first.NextCursor);
        Assert.Equal(
            Enumerable.Range(0, 4).Select(index => $"message-{index}"),
            second.Items.Select(message => message.Content));
        Assert.False(second.HasMore);
        Assert.Null(second.NextCursor);
        Assert.Empty(first.Items.Select(message => message.MessageId)
            .Intersect(second.Items.Select(message => message.MessageId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public async Task GetMessagesAsync_RejectsLimitOutsideOneToEight(int limit)
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "summary");
        var service = new ChatService(
            dbContext,
            new QueueClassifierClient(CreateResponse("unused", "unused")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetMessagesAsync(
                session.Id,
                session.UserId,
                limit,
                cursor: null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMessagesAsync_RejectsMalformedCursor()
    {
        await using var dbContext = CreateContext();
        var session = SeedSession(dbContext, "summary");
        var service = new ChatService(
            dbContext,
            new QueueClassifierClient(CreateResponse("unused", "unused")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetMessagesAsync(
                session.Id,
                session.UserId,
                limit: 8,
                cursor: "not-a-cursor",
                TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static ChatSession SeedSession(
        AppDbContext dbContext,
        string symptomSummary)
    {
        var session = new ChatSession
        {
            UserId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            PetType = PetType.GuineaPig,
            SymptomSummary = symptomSummary,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        dbContext.ChatSessions.Add(session);
        dbContext.SaveChanges();
        return session;
    }

    private static ClassifierChatResponse CreateResponse(
        string answer,
        string symptomSummary)
    {
        return new ClassifierChatResponse
        {
            Mode = ClassifierChatMode.General,
            Answer = answer,
            SymptomSummary = symptomSummary,
            Disclaimer = "Veterinary disclaimer"
        };
    }

    private sealed class QueueClassifierClient(params ClassifierChatResponse[] responses)
        : IClassifierClient
    {
        private readonly Queue<ClassifierChatResponse> responses = new(responses);

        public List<ClassifierChatRequest> Requests { get; } = [];

        public Task<ClassifierChatResponse> ChatAsync(
            ClassifierChatRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Dequeue());
        }

        public Task<ClassifierNutritionResponse> AnalyzeNutritionAsync(
            ClassifierNutritionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Chat tests do not analyse nutrition.");
    }

    private sealed class ControlledClassifierClient : IClassifierClient
    {
        private readonly TaskCompletionSource requestStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<ClassifierChatResponse> response = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestStarted => requestStarted.Task;

        public Task<ClassifierChatResponse> ChatAsync(
            ClassifierChatRequest request,
            CancellationToken cancellationToken = default)
        {
            requestStarted.TrySetResult();
            return response.Task.WaitAsync(cancellationToken);
        }

        public Task<ClassifierNutritionResponse> AnalyzeNutritionAsync(
            ClassifierNutritionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Chat tests do not analyse nutrition.");

        public void Complete(ClassifierChatResponse classifierResponse)
        {
            response.TrySetResult(classifierResponse);
        }
    }

    private sealed class ThrowingClassifierClient(Exception? exception = null)
        : IClassifierClient
    {
        private readonly Exception exception = exception
            ?? new ClassifierUnavailableException("Classifier is unavailable.");

        public Task<ClassifierChatResponse> ChatAsync(
            ClassifierChatRequest request,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public Task<ClassifierNutritionResponse> AnalyzeNutritionAsync(
            ClassifierNutritionRequest request,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }
}
