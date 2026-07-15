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

        await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
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
        Assert.Equal("last good summary", persistedSession.SymptomSummary);
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
    }

    private sealed class ThrowingClassifierClient : IClassifierClient
    {
        public Task<ClassifierChatResponse> ChatAsync(
            ClassifierChatRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new ClassifierUnavailableException(
                "Classifier is unavailable.");
        }
    }
}
