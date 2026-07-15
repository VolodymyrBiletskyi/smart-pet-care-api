using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Infrastructure.Classifier;

public sealed class ClassifierClient : IClassifierClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly HttpClient httpClient;
    private readonly ClassifierMetrics? metrics;
    private readonly ClassifierCircuitBreaker? circuitBreaker;

    public ClassifierClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public ClassifierClient(
        HttpClient httpClient,
        ClassifierMetrics metrics,
        ClassifierCircuitBreaker circuitBreaker)
    {
        this.httpClient = httpClient;
        this.metrics = metrics;
        this.circuitBreaker = circuitBreaker;
    }

    public async Task<ClassifierChatResponse> ChatAsync(
        ClassifierChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var lease = circuitBreaker?.TryAcquire()
            ?? new ClassifierCircuitLease(IsAllowed: true);
        if (!lease.IsAllowed)
        {
            metrics?.RecordFailure(
                "circuit_open",
                StatusCodes.Status503ServiceUnavailable,
                "circuit_open",
                "circuit_breaker");
            throw new ClassifierUnavailableException(
                "The classifier circuit is open.",
                HttpStatusCode.ServiceUnavailable,
                code: "circuit_open",
                retryAfterSeconds: lease.RetryAfterSeconds);
        }

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "chat",
                request,
                SerializerOptions,
                cancellationToken);

            var result = await HandleResponseAsync(response, cancellationToken);
            circuitBreaker?.RecordSuccess();
            return result;
        }
        catch (ClassifierInvalidResponseException)
        {
            circuitBreaker?.RecordSuccess();
            throw;
        }
        catch (ClassifierRateLimitedException exception)
        {
            circuitBreaker?.RecordSuccess();
            var kind = string.Equals(
                exception.Code,
                "quota_exhausted",
                StringComparison.OrdinalIgnoreCase)
                ? "quota_exhausted"
                : "rate_limited";
            metrics?.RecordFailure(
                kind,
                StatusCodes.Status429TooManyRequests,
                exception.Code,
                "classifier");
            throw;
        }
        catch (ClassifierUnavailableException exception)
        {
            var isQuotaExhausted = string.Equals(
                exception.Code,
                "quota_exhausted",
                StringComparison.OrdinalIgnoreCase);
            if (isQuotaExhausted)
            {
                circuitBreaker?.RecordSuccess();
            }
            else
            {
                circuitBreaker?.RecordAvailabilityFailure();
            }

            metrics?.RecordFailure(
                isQuotaExhausted
                    ? "quota_exhausted"
                    : "service_unavailable",
                (int?)exception.StatusCode
                    ?? StatusCodes.Status503ServiceUnavailable,
                exception.Code ?? "service_unavailable",
                "classifier");
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            circuitBreaker?.RecordAvailabilityFailure();
            metrics?.RecordFailure(
                "timeout",
                StatusCodes.Status503ServiceUnavailable,
                "request_timeout",
                "transport");
            throw new ClassifierUnavailableException(
                "The classifier request timed out.",
                innerException: exception,
                code: "request_timeout");
        }
        catch (HttpRequestException exception)
        {
            circuitBreaker?.RecordAvailabilityFailure();
            metrics?.RecordFailure(
                "network_error",
                StatusCodes.Status503ServiceUnavailable,
                "classifier_unavailable",
                "transport");
            throw new ClassifierUnavailableException(
                "The classifier could not be reached.",
                exception.StatusCode,
                exception,
                code: "classifier_unavailable");
        }
        catch (IOException exception)
        {
            circuitBreaker?.RecordAvailabilityFailure();
            metrics?.RecordFailure(
                "response_read_error",
                StatusCodes.Status503ServiceUnavailable,
                "response_read_failed",
                "transport");
            throw new ClassifierUnavailableException(
                "The classifier response could not be read.",
                innerException: exception,
                code: "response_read_failed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            circuitBreaker?.RecordNeutralOutcome();
            throw;
        }
        catch
        {
            circuitBreaker?.RecordNeutralOutcome();
            throw;
        }
    }

    private static async Task<ClassifierChatResponse> HandleResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var error = TryDeserializeError(content);
            throw new ClassifierRateLimitedException(
                "The classifier rate limit was exceeded.",
                error?.Code ?? "rate_limit_exceeded",
                GetRetryAfterSeconds(response, error));
        }

        if ((int)response.StatusCode >= 500)
        {
            var error = TryDeserializeError(content);
            throw new ClassifierUnavailableException(
                "The classifier is temporarily unavailable.",
                response.StatusCode,
                code: error?.Code ?? "service_unavailable",
                retryAfterSeconds: GetRetryAfterSeconds(response, error));
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode == HttpStatusCode.UnprocessableEntity
                ? "The classifier rejected the generated request."
                : "The classifier returned an unexpected status code.";

            throw new ClassifierInvalidResponseException(
                message,
                response.StatusCode,
                content);
        }

        try
        {
            var result = JsonSerializer.Deserialize<ClassifierChatResponse>(
                content,
                SerializerOptions);
            ValidateResponse(result, response.StatusCode);
            return result!;
        }
        catch (JsonException exception)
        {
            throw MalformedResponse(response.StatusCode, exception);
        }
        catch (NotSupportedException exception)
        {
            throw MalformedResponse(response.StatusCode, exception);
        }
    }

    private static ClassifierErrorResponse? TryDeserializeError(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ClassifierErrorResponse>(
                content,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static int? GetRetryAfterSeconds(
        HttpResponseMessage response,
        ClassifierErrorResponse? error)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return ToRetryAfterSeconds(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            return ToRetryAfterSeconds(date - DateTimeOffset.UtcNow);
        }

        return error?.RetryAfterSeconds is >= 0
            ? error.RetryAfterSeconds
            : null;
    }

    private static int ToRetryAfterSeconds(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 0;
        }

        return duration.TotalSeconds >= int.MaxValue
            ? int.MaxValue
            : (int)Math.Ceiling(duration.TotalSeconds);
    }

    private static void ValidateResponse(
        ClassifierChatResponse? response,
        HttpStatusCode statusCode)
    {
        if (response is null
            || !Enum.IsDefined(response.Mode)
            || response.Answer is null
            || response.SymptomSummary is null
            || response.RelatedTopics is null
            || response.Disclaimer is null)
        {
            throw MalformedResponse(statusCode);
        }

        var prediction = response.Prediction;
        if (prediction is null)
        {
            return;
        }

        if (prediction.PredictedCondition is null
            || !double.IsFinite(prediction.Confidence)
            || prediction.TopK is null
            || !Enum.IsDefined(prediction.Urgency)
            || prediction.Specialist is null
            || prediction.DiseaseCategory is null
            || prediction.HomeAdvice is null
            || prediction.TopK.Any(item =>
                item is null
                || item.Condition is null
                || !double.IsFinite(item.Confidence)))
        {
            throw MalformedResponse(statusCode);
        }
    }

    private static ClassifierInvalidResponseException MalformedResponse(
        HttpStatusCode statusCode,
        Exception? innerException = null)
    {
        return new ClassifierInvalidResponseException(
            "The classifier returned a malformed response.",
            statusCode,
            innerException: innerException);
    }
}
