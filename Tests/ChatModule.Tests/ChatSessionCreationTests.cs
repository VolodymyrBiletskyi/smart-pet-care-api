using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Domain;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ChatSessionCreationTests
{
    [Fact]
    public async Task CreateSessionAsync_CalledTwiceForPet_ReplacesSessionAndHistory()
    {
        await using var dbContext = CreateContext();
        var userId = Guid.NewGuid();
        var pet = SeedPet(dbContext, userId, Enums.AnimalSpecies.GuineaPig);
        var service = new ChatService(dbContext, new UnusedClassifierClient());

        var first = await service.CreateSessionAsync(
            userId,
            pet.Id,
            TestContext.Current.CancellationToken);
        dbContext.ChatMessages.Add(new ChatMessage
        {
            SessionId = first.SessionId,
            Role = ChatMessageRole.User,
            Content = "old message"
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var second = await service.CreateSessionAsync(
            userId,
            pet.Id,
            TestContext.Current.CancellationToken);

        var session = await dbContext.ChatSessions.SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(second.SessionId, session.Id);
        Assert.Equal(userId, session.UserId);
        Assert.Equal(pet.Id, session.PetId);
        Assert.Equal(PetType.GuineaPig, session.PetType);
        Assert.Null(session.SymptomSummary);
        Assert.Empty(await dbContext.ChatMessages.ToListAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSessionAsync_ForAnotherUsersPet_ReturnsNotFound()
    {
        await using var dbContext = CreateContext();
        var ownerId = Guid.NewGuid();
        var pet = SeedPet(dbContext, ownerId, Enums.AnimalSpecies.Dog);
        var service = new ChatService(dbContext, new UnusedClassifierClient());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateSessionAsync(
                Guid.NewGuid(),
                pet.Id,
                TestContext.Current.CancellationToken));

        Assert.Empty(await dbContext.ChatSessions.ToListAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSessionsAndGetSession_ReturnOnlyOwnedSessionWithOrderedMessages()
    {
        await using var dbContext = CreateContext();
        var userId = Guid.NewGuid();
        var pet = SeedPet(dbContext, userId, Enums.AnimalSpecies.Dog);
        var session = new ChatSession
        {
            UserId = userId,
            PetId = pet.Id,
            PetType = PetType.Dog,
            SymptomSummary = "summary"
        };
        dbContext.ChatSessions.Add(session);
        dbContext.ChatMessages.AddRange(
            new ChatMessage
            {
                SessionId = session.Id,
                Role = ChatMessageRole.Assistant,
                Content = "second",
                CreatedAt = DateTime.UtcNow
            },
            new ChatMessage
            {
                SessionId = session.Id,
                Role = ChatMessageRole.User,
                Content = "first",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ChatService(dbContext, new UnusedClassifierClient());

        var sessions = await service.GetSessionsAsync(
            userId,
            TestContext.Current.CancellationToken);
        var details = await service.GetSessionAsync(
            session.Id,
            userId,
            TestContext.Current.CancellationToken);

        Assert.Single(sessions);
        Assert.Equal("summary", sessions[0].SymptomSummary);
        Assert.Equal(["first", "second"], details.Messages.Select(message => message.Content));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetSessionAsync(
                session.Id,
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static Pet SeedPet(
        AppDbContext dbContext,
        Guid userId,
        Enums.AnimalSpecies species)
    {
        var pet = new Pet
        {
            UserId = userId,
            Name = "Chat test pet",
            Species = species
        };
        dbContext.Pets.Add(pet);
        dbContext.SaveChanges();
        return pet;
    }

    private sealed class UnusedClassifierClient : IClassifierClient
    {
        public Task<ClassifierFeedingSummaryResponse> SummarizeFeedingAsync(
            ClassifierFeedingSummaryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ClassifierFeedingSummaryResponse>(
                new NotSupportedException());
        public Task<ClassifierChatResponse> ChatAsync(
            ClassifierChatRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Session queries must not call the classifier.");
        }
    }
}
