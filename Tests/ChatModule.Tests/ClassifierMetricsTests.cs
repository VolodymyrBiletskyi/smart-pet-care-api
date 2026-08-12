using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

[Collection(ClassifierMetricsTestCollection.Name)]
public sealed class ClassifierMetricsTests
{
    [Fact]
    public async Task ClassifierClient_Records429Quota503AndTimeoutMetrics()
    {
        var measurements = new List<FailureMeasurement>();
        using var listener = CreateListener(measurements);
        using var metrics = new ClassifierMetrics();
        var circuitBreaker = CreateCircuitBreaker();

        await InvokeAndIgnoreAsync(CreateClient(
            JsonResponse(
                HttpStatusCode.TooManyRequests,
                """{"code":"rate_limit_exceeded","message":"limited","retryable":true}"""),
            metrics,
            circuitBreaker));
        await InvokeAndIgnoreAsync(CreateClient(
            JsonResponse(
                HttpStatusCode.TooManyRequests,
                """{"code":"quota_exhausted","message":"quota","retryable":true}"""),
            metrics,
            circuitBreaker));
        await InvokeAndIgnoreAsync(CreateClient(
            JsonResponse(
                HttpStatusCode.ServiceUnavailable,
                """{"code":"service_overloaded","message":"busy","retryable":true}"""),
            metrics,
            circuitBreaker));

        using var timeoutHttpClient = new HttpClient(new BlockingHandler())
        {
            BaseAddress = new Uri("https://classifier.example/"),
            Timeout = TimeSpan.FromMilliseconds(50)
        };
        var timeoutClient = new ClassifierClient(
            timeoutHttpClient,
            metrics,
            circuitBreaker);
        await InvokeAndIgnoreAsync(timeoutClient);

        Assert.Contains(measurements, measurement =>
            measurement.Kind == "rate_limited"
            && measurement.StatusCode == 429
            && measurement.Code == "rate_limit_exceeded");
        Assert.Contains(measurements, measurement =>
            measurement.Kind == "quota_exhausted"
            && measurement.StatusCode == 429
            && measurement.Code == "quota_exhausted");
        Assert.Contains(measurements, measurement =>
            measurement.Kind == "service_unavailable"
            && measurement.StatusCode == 503
            && measurement.Code == "service_overloaded");
        Assert.Contains(measurements, measurement =>
            measurement.Kind == "timeout"
            && measurement.StatusCode == 503
            && measurement.Code == "request_timeout");
    }

    private static MeterListener CreateListener(
        ICollection<FailureMeasurement> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == ClassifierMetrics.MeterName
                    && instrument.Name == ClassifierMetrics.FailureCounterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var values = tags.ToArray();
            measurements.Add(new FailureMeasurement(
                value,
                GetString(values, "kind"),
                GetInt(values, "status_code"),
                GetString(values, "code"),
                GetString(values, "source")));
        });
        listener.Start();
        return listener;
    }

    private static string GetString(
        KeyValuePair<string, object?>[] tags,
        string name)
    {
        return Assert.IsType<string>(tags.Single(tag => tag.Key == name).Value);
    }

    private static int GetInt(
        KeyValuePair<string, object?>[] tags,
        string name)
    {
        return Assert.IsType<int>(tags.Single(tag => tag.Key == name).Value);
    }

    private static ClassifierCircuitBreaker CreateCircuitBreaker()
    {
        return new ClassifierCircuitBreaker(
            Options.Create(new ClassifierOptions
            {
                CircuitBreakerFailureThreshold = 10,
                CircuitBreakerBreakSeconds = 30
            }),
            TimeProvider.System);
    }

    private static ClassifierClient CreateClient(
        HttpResponseMessage response,
        ClassifierMetrics metrics,
        ClassifierCircuitBreaker circuitBreaker)
    {
        var httpClient = new HttpClient(new ResponseHandler(response))
        {
            BaseAddress = new Uri("https://classifier.example/")
        };
        return new ClassifierClient(httpClient, metrics, circuitBreaker);
    }

    private static async Task InvokeAndIgnoreAsync(ClassifierClient client)
    {
        await Assert.ThrowsAnyAsync<Exception>(() => client.ChatAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken));
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

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private sealed class ResponseHandler(HttpResponseMessage response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        }
    }

    private sealed record FailureMeasurement(
        long Value,
        string Kind,
        int StatusCode,
        string Code,
        string Source);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ClassifierMetricsTestCollection
{
    public const string Name = "Classifier metrics tests";
}
