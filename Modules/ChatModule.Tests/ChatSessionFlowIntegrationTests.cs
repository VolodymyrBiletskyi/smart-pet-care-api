using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Domain;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ChatSessionFlowIntegrationTests
{
    [Fact]
    public async Task CreateMessageReadAndReplaceSession_PersistsExpectedState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(
            TestContext.Current.CancellationToken);

        var user = new User
        {
            Email = "session-flow@example.com",
            PasswordHash = "hash"
        };
        var pet = new Pet
        {
            UserId = user.Id,
            Name = "Buddy",
            Species = "Dog"
        };
        dbContext.AddRange(user, pet);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var classifier = new RecordingClassifierClient();
        var service = new ChatService(dbContext, classifier);
        var created = await service.CreateSessionAsync(
            user.Id,
            pet.Id,
            TestContext.Current.CancellationToken);

        var response = await service.HandleUserMessageAsync(
            created.SessionId,
            user.Id,
            "Buddy is lethargic",
            TestContext.Current.CancellationToken);
        var details = await service.GetSessionAsync(
            created.SessionId,
            user.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal("Classifier answer", response.Answer);
        Assert.Equal("updated summary", details.SymptomSummary);
        Assert.Equal(2, details.Messages.Count);
        Assert.Equal(ChatMessageRole.User, details.Messages[0].Role);
        Assert.Equal(ChatMessageRole.Assistant, details.Messages[1].Role);

        var request = Assert.Single(classifier.Requests);
        Assert.Equal(created.SessionId.ToString("D"), request.SessionId);
        Assert.Equal(ClassifierPetType.Dog, request.PetType);
        Assert.Null(request.SymptomSummary);
        Assert.Equal("Buddy is lethargic", Assert.Single(request.Messages).Content);

        var replacement = await service.CreateSessionAsync(
            user.Id,
            pet.Id,
            TestContext.Current.CancellationToken);

        Assert.NotEqual(created.SessionId, replacement.SessionId);
        Assert.Empty(await dbContext.ChatMessages.ToListAsync(
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetSessionAsync(
                created.SessionId,
                user.Id,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMessages_WithCursor_ExecutesAsRelationalQuery()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(
            TestContext.Current.CancellationToken);

        var user = new User
        {
            Email = "history-flow@example.com",
            PasswordHash = "hash"
        };
        var pet = new Pet
        {
            UserId = user.Id,
            Name = "Buddy",
            Species = "Dog"
        };
        var session = new ChatSession
        {
            UserId = user.Id,
            PetId = pet.Id,
            PetType = PetType.Dog
        };
        dbContext.AddRange(user, pet, session);
        var start = DateTime.UtcNow.AddHours(-1);
        for (var index = 0; index < 10; index++)
        {
            dbContext.ChatMessages.Add(new ChatMessage
            {
                SessionId = session.Id,
                Role = ChatMessageRole.User,
                Content = $"message-{index}",
                CreatedAt = start.AddMinutes(index)
            });
        }
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ChatService(
            dbContext,
            new RecordingClassifierClient());

        var first = await service.GetMessagesAsync(
            session.Id,
            user.Id,
            limit: 8,
            cursor: null,
            TestContext.Current.CancellationToken);
        var second = await service.GetMessagesAsync(
            session.Id,
            user.Id,
            limit: 8,
            first.NextCursor,
            TestContext.Current.CancellationToken);

        Assert.Equal(8, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Equal("message-0", second.Items[0].Content);
        Assert.Equal("message-1", second.Items[1].Content);
    }

    private sealed class RecordingClassifierClient : IClassifierClient
    {
        public List<ClassifierChatRequest> Requests { get; } = [];

        public Task<ClassifierChatResponse> ChatAsync(
            ClassifierChatRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new ClassifierChatResponse
            {
                Mode = ClassifierChatMode.Health,
                Answer = "Classifier answer",
                SymptomSummary = "updated summary",
                Disclaimer = "Veterinary disclaimer",
                Prediction = new ClassifierChatPrediction
                {
                    PredictedCondition = "condition",
                    Confidence = 0.75,
                    Urgency = ClassifierUrgency.Monitor,
                    Specialist = "veterinarian",
                    DiseaseCategory = "general",
                    HomeAdvice = ["Monitor Buddy"]
                }
            });
        }
    }
}
