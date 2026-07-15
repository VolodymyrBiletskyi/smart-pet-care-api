using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Api;
using smart_pet_care_api.Modules.ChatModule.Domain;
using smart_pet_care_api.Modules.ChatModule.DTOs;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ChatControllersTests
{
    [Fact]
    public async Task CreateSession_Returns201WithLocation()
    {
        var result = CreateSessionResult();
        var controller = CreateSessionsController(
            new StubChatService { CreateResult = result });

        var action = await controller.CreateSession(
            new CreateChatSessionRequest { PetId = result.PetId },
            TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(action);
        Assert.Equal($"/api/sessions/{result.SessionId:D}", created.Location);
        var response = Assert.IsType<ChatSessionResponseDto>(created.Value);
        Assert.Equal(result.SessionId, response.SessionId);
    }

    [Fact]
    public async Task GetSessions_ReturnsOwnedSessionSummaries()
    {
        var result = CreateSessionResult();
        var controller = CreateSessionsController(
            new StubChatService { Sessions = [result] });

        var action = await controller.GetSessions(
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(action);
        var sessions = Assert.IsAssignableFrom<IEnumerable<ChatSessionResponseDto>>(
            ok.Value);
        Assert.Equal(result.SessionId, Assert.Single(sessions).SessionId);
    }

    [Fact]
    public async Task GetSession_WhenNotOwned_Returns404()
    {
        var controller = CreateSessionsController(
            new StubChatService { ThrowSessionNotFound = true });

        var action = await controller.GetSession(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public async Task PostMessage_ReturnsFlattenedClassifierResponse()
    {
        var service = new StubChatService
        {
            MessageResult = new ClassifierChatResponse
            {
                Mode = ClassifierChatMode.General,
                Answer = "answer",
                SymptomSummary = "summary",
                Disclaimer = "disclaimer"
            }
        };
        var controller = CreateMessagesController(service);

        var action = await controller.PostMessage(
            Guid.NewGuid(),
            new PostSessionMessageRequest { Text = "question" },
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<SessionMessageResponseDto>(ok.Value);
        Assert.Equal("answer", response.Answer);
    }

    [Fact]
    public async Task GetMessages_ReturnsPageWithMaximumEightItems()
    {
        var sessionId = Guid.NewGuid();
        var service = new StubChatService
        {
            MessagePageResult = new ChatMessagePageResult(
                sessionId,
                [],
                8,
                HasMore: false,
                NextCursor: null)
        };
        var controller = CreateMessagesController(service);

        var action = await controller.GetMessages(
            sessionId,
            TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<SessionMessagesPageResponseDto>(ok.Value);
        Assert.Equal(8, response.Pagination.Limit);
        Assert.False(response.Pagination.HasMore);
    }

    [Fact]
    public async Task PostMessage_WhenClassifierUnavailable_Returns503()
    {
        var controller = CreateMessagesController(
            new StubChatService { ThrowClassifierUnavailable = true });

        var action = await controller.PostMessage(
            Guid.NewGuid(),
            new PostSessionMessageRequest { Text = "question" },
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    private static ChatSessionsController CreateSessionsController(
        IChatService service)
    {
        return WithUser(new ChatSessionsController(service));
    }

    private static SessionMessagesController CreateMessagesController(
        IChatService service)
    {
        return WithUser(new SessionMessagesController(
            service,
            NullLogger<SessionMessagesController>.Instance));
    }

    private static T WithUser<T>(T controller)
        where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [new Claim("userId", Guid.NewGuid().ToString())],
                        "test"))
            }
        };
        return controller;
    }

    private static ChatSessionResult CreateSessionResult()
    {
        var now = DateTime.UtcNow;
        return new ChatSessionResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PetType.Dog,
            null,
            now,
            now);
    }

    private sealed class StubChatService : IChatService
    {
        public IReadOnlyList<ChatSessionResult> Sessions { get; init; } = [];
        public ChatSessionResult? CreateResult { get; init; }
        public ClassifierChatResponse? MessageResult { get; init; }
        public ChatMessagePageResult? MessagePageResult { get; init; }
        public bool ThrowSessionNotFound { get; init; }
        public bool ThrowClassifierUnavailable { get; init; }

        public Task<IReadOnlyList<ChatSessionResult>> GetSessionsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Sessions);
        }

        public Task<ChatSessionDetailsResult> GetSessionAsync(
            Guid sessionId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (ThrowSessionNotFound)
            {
                throw new KeyNotFoundException("The chat session was not found.");
            }

            var result = CreateResult ?? CreateSessionResult();
            return Task.FromResult(new ChatSessionDetailsResult(
                result.SessionId,
                result.PetId,
                result.PetType,
                result.SymptomSummary,
                result.CreatedAt,
                result.UpdatedAt,
                []));
        }

        public Task<ChatSessionResult> CreateSessionAsync(
            Guid userId,
            Guid petId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateResult ?? CreateSessionResult());
        }

        public Task<ChatMessagePageResult> GetMessagesAsync(
            Guid sessionId,
            Guid userId,
            int limit,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                MessagePageResult
                ?? new ChatMessagePageResult(
                    sessionId,
                    [],
                    limit,
                    HasMore: false,
                    NextCursor: null));
        }

        public Task<ClassifierChatResponse> HandleUserMessageAsync(
            Guid sessionId,
            Guid userId,
            string userText,
            CancellationToken cancellationToken = default)
        {
            if (ThrowClassifierUnavailable)
            {
                throw new ClassifierUnavailableException(
                    "Classifier is unavailable.");
            }

            return Task.FromResult(MessageResult ?? throw new InvalidOperationException());
        }
    }
}
