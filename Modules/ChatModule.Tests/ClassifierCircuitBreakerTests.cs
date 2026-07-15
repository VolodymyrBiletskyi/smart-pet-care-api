using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ClassifierCircuitBreakerTests
{
    [Fact]
    public void CircuitBreaker_AfterThreshold_OpensForConfiguredDuration()
    {
        var timeProvider = new TestTimeProvider();
        var circuitBreaker = CreateCircuitBreaker(timeProvider);

        Assert.True(circuitBreaker.TryAcquire().IsAllowed);
        circuitBreaker.RecordAvailabilityFailure();
        Assert.True(circuitBreaker.TryAcquire().IsAllowed);
        circuitBreaker.RecordAvailabilityFailure();

        var rejected = circuitBreaker.TryAcquire();
        Assert.False(rejected.IsAllowed);
        Assert.Equal(30, rejected.RetryAfterSeconds);
    }

    [Fact]
    public void CircuitBreaker_AfterBreakDuration_AllowsOneProbeAndSuccessClosesCircuit()
    {
        var timeProvider = new TestTimeProvider();
        var circuitBreaker = CreateCircuitBreaker(timeProvider);
        circuitBreaker.RecordAvailabilityFailure();
        circuitBreaker.RecordAvailabilityFailure();
        timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.True(circuitBreaker.TryAcquire().IsAllowed);
        var concurrentProbe = circuitBreaker.TryAcquire();
        Assert.False(concurrentProbe.IsAllowed);
        Assert.Equal(1, concurrentProbe.RetryAfterSeconds);

        circuitBreaker.RecordSuccess();

        Assert.True(circuitBreaker.TryAcquire().IsAllowed);
    }

    [Fact]
    public async Task ClassifierClient_WhenCircuitIsOpen_DoesNotCallPython()
    {
        var timeProvider = new TestTimeProvider();
        var circuitBreaker = CreateCircuitBreaker(timeProvider);
        circuitBreaker.RecordAvailabilityFailure();
        circuitBreaker.RecordAvailabilityFailure();
        var handler = new CountingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://classifier.example/")
        };
        using var metrics = new ClassifierMetrics();
        var client = new ClassifierClient(httpClient, metrics, circuitBreaker);

        var exception = await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            client.ChatAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Equal("circuit_open", exception.Code);
        Assert.Equal(30, exception.RetryAfterSeconds);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ClassifierClient_QuotaExhausted_DoesNotOpenCircuit()
    {
        var timeProvider = new TestTimeProvider();
        var circuitBreaker = CreateCircuitBreaker(
            timeProvider,
            failureThreshold: 1);
        var handler = new QueueHandler(
            JsonResponse(
                HttpStatusCode.ServiceUnavailable,
                """{"code":"quota_exhausted","message":"quota","retryable":true}"""),
            JsonResponse(
                HttpStatusCode.OK,
                """
                {
                  "mode":"general",
                  "answer":"answer",
                  "symptomSummary":"summary",
                  "relatedTopics":[],
                  "needsClarification":false,
                  "disclaimer":"disclaimer"
                }
                """));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://classifier.example/")
        };
        using var metrics = new ClassifierMetrics();
        var client = new ClassifierClient(httpClient, metrics, circuitBreaker);

        await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            client.ChatAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));
        var response = await client.ChatAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("answer", response.Answer);
        Assert.Equal(2, handler.RequestCount);
    }

    private static ClassifierCircuitBreaker CreateCircuitBreaker(
        TimeProvider timeProvider,
        int failureThreshold = 2)
    {
        return new ClassifierCircuitBreaker(
            Options.Create(new ClassifierOptions
            {
                CircuitBreakerFailureThreshold = failureThreshold,
                CircuitBreakerBreakSeconds = 30
            }),
            timeProvider);
    }

    private static ClassifierChatRequest CreateRequest()
    {
        return new ClassifierChatRequest
        {
            Messages =
            [
                new ClassifierChatMessage
                {
                    Role = ClassifierChatRole.User,
                    Content = "Hello"
                }
            ]
        };
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private sealed class QueueHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responses.Dequeue());
        }
    }
}
