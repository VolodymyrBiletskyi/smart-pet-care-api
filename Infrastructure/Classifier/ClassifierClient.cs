using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Infrastructure.Classifier;

public sealed class ClassifierClient(HttpClient httpClient) : IClassifierClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ClassifierChatResponse> ChatAsync(
        ClassifierChatRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "chat",
                request,
                SerializerOptions,
                cancellationToken);

            return await HandleResponseAsync(response, cancellationToken);
        }
        catch (ClassifierInvalidResponseException)
        {
            throw;
        }
        catch (ClassifierUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ClassifierUnavailableException(
                "The classifier request timed out.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ClassifierUnavailableException(
                "The classifier could not be reached.",
                exception.StatusCode,
                exception);
        }
        catch (IOException exception)
        {
            throw new ClassifierUnavailableException(
                "The classifier response could not be read.",
                innerException: exception);
        }
    }

    private static async Task<ClassifierChatResponse> HandleResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if ((int)response.StatusCode >= 500)
        {
            throw new ClassifierUnavailableException(
                "The classifier is temporarily unavailable.",
                response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
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
