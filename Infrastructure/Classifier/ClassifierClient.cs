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

    public ClassifierClient(
        HttpClient httpClient,
        ClassifierMetrics? metrics = null,
        ClassifierCircuitBreaker? circuitBreaker = null)
    {
        this.httpClient = httpClient;
        this.metrics = metrics;
        this.circuitBreaker = circuitBreaker;
    }

    public Task<ClassifierChatResponse> ChatAsync(
        ClassifierChatRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ClassifierChatRequest, ClassifierChatResponse>(
            "chat",
            request,
            ValidateChatResponse,
            cancellationToken);
    }

    public Task<ClassifierFeedingSummaryResponse> SummarizeFeedingAsync(
        ClassifierFeedingSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ClassifierFeedingSummaryRequest, ClassifierFeedingSummaryResponse>(
            "feeding-summary",
            request,
            ValidateFeedingSummaryResponse,
            cancellationToken);
    }

    /// <summary>
    /// Runs one classifier call through the shared circuit breaker, failure
    /// metrics and public-status mapping. Every classifier route must go
    /// through here so a single unhealthy route cannot bypass the breaker.
    /// </summary>
    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        Func<TResponse?, string?> validate,
        CancellationToken cancellationToken)
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
                path,
                request,
                SerializerOptions,
                cancellationToken);

            var result = await HandleResponseAsync(response, validate, cancellationToken);
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

    private static async Task<TResponse> HandleResponseAsync<TResponse>(
        HttpResponseMessage response,
        Func<TResponse?, string?> validate,
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

        TResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<TResponse>(
                content,
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw MalformedResponse(
                response.StatusCode,
                DescribeJsonFailure(exception),
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw MalformedResponse(response.StatusCode, "body is not deserializable", exception);
        }

        var reason = validate(result);
        return reason is null
            ? result!
            : throw MalformedResponse(response.StatusCode, reason);
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

    /// <summary>
    /// Serializer messages name types, properties and JSON paths but never
    /// property values, so they are safe for normal application logs. The
    /// trailing line/byte position is dropped as noise.
    /// </summary>
    private static string DescribeJsonFailure(JsonException exception)
    {
        var message = exception.Message;
        var noiseIndex = message.IndexOf(" | LineNumber:", StringComparison.Ordinal);
        return noiseIndex < 0 ? message : message[..noiseIndex];
    }

    /// <summary>Returns null when valid, otherwise why the contract was broken.</summary>
    private static string? ValidateChatResponse(ClassifierChatResponse? response)
    {
        if (response is null)
        {
            return "body was null or empty";
        }

        if (!Enum.IsDefined(response.Mode))
        {
            return "mode is missing or not one of general/health/emergency";
        }

        if (response.Answer is null)
        {
            return "answer is missing";
        }

        if (response.SymptomSummary is null)
        {
            return "symptomSummary is missing";
        }

        if (response.RelatedTopics is null)
        {
            return "relatedTopics is missing";
        }

        if (response.Disclaimer is null)
        {
            return "disclaimer is missing";
        }

        var prediction = response.Prediction;
        if (prediction is null)
        {
            return null;
        }

        if (prediction.PredictedCondition is null)
        {
            return "prediction.predictedCondition is missing";
        }

        if (!double.IsFinite(prediction.Confidence))
        {
            return "prediction.confidence is not a finite number";
        }

        if (prediction.TopK is null)
        {
            return "prediction.topK is missing";
        }

        if (!Enum.IsDefined(prediction.Urgency))
        {
            return "prediction.urgency is missing or not one of MONITOR/CONSULT_SOON/URGENT/EMERGENCY";
        }

        if (prediction.Specialist is null)
        {
            return "prediction.specialist is missing";
        }

        if (prediction.DiseaseCategory is null)
        {
            return "prediction.diseaseCategory is missing";
        }

        if (prediction.HomeAdvice is null)
        {
            return "prediction.homeAdvice is missing";
        }

        if (prediction.TopK.Any(item => item is null || item.Condition is null))
        {
            return "prediction.topK contains an entry without a condition";
        }

        return prediction.TopK.Any(item => !double.IsFinite(item.Confidence))
            ? "prediction.topK contains a non-finite confidence"
            : null;
    }

    /// <summary>Returns null when valid, otherwise why the contract was broken.</summary>
    private static string? ValidateFeedingSummaryResponse(ClassifierFeedingSummaryResponse? response)
    {
        if (response is null)
        {
            return "body was null or empty";
        }

        if (response.Results is null)
        {
            return "results is missing";
        }

        // Every request carries at least one pet, so an empty result set never
        // answers the question that was asked.
        if (response.Results.Count == 0)
        {
            return "results is empty";
        }

        foreach (var result in response.Results)
        {
            if (result is null)
            {
                return "results contains a null entry";
            }

            if (string.IsNullOrEmpty(result.PetId))
            {
                return "results contains an entry without a petId";
            }

            if (!Enum.IsDefined(result.Status))
            {
                return "results contains an entry whose status is missing or not one of "
                    + "EXTREME_UNDER_TARGET/UNDER_TARGET/ON_TARGET/OVER_TARGET/EXTREME_OVER_TARGET";
            }
        }

        return response.Disclaimer is null
            ? "disclaimer is missing"
            : null;
    }

    private static ClassifierInvalidResponseException MalformedResponse(
        HttpStatusCode statusCode,
        string? validationReason = null,
        Exception? innerException = null)
    {
        return new ClassifierInvalidResponseException(
            "The classifier returned a malformed response.",
            statusCode,
            innerException: innerException,
            validationReason: validationReason);
    }
}
