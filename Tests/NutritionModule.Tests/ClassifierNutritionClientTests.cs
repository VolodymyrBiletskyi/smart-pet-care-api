using System.Net;
using System.Text;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using Xunit;

namespace smart_pet_care_api.Modules.NutritionModule.Tests;

/// <summary>
/// Covers the classifier responses that are reported to clients as 502
/// <c>classifier_invalid_response</c>, and the reason recorded for each.
/// </summary>
public sealed class ClassifierNutritionClientTests
{
    private const string ValidBody = """
        {
          "grade": "B",
          "score": 78,
          "summary": "Slightly under the calorie target.",
          "advice": ["Add a small evening meal."],
          "disclaimer": "Not veterinary advice."
        }
        """;

    [Fact]
    public async Task AnalyzeNutritionAsync_PostsRelativeNutritionPathWithApiKey()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, ValidBody));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var response = await client.AnalyzeNutritionAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ClassifierNutritionGrade.B, response.Grade);
        Assert.Equal(78, response.Score);
        Assert.Equal(
            new Uri("https://classifier.example/Prod/nutrition-analysis"),
            handler.RequestUri);
        Assert.Equal("test-key", handler.ApiKey);
    }

    [Fact]
    public async Task AnalyzeNutritionAsync_RejectsLowercaseGrade()
    {
        // Grade values are matched exactly; "b" is not "B".
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            ValidBody.Replace("\"B\"", "\"b\"")));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.AnalyzeNutritionAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains("grade", exception.ValidationReason);
    }

    [Fact]
    public async Task AnalyzeNutritionAsync_WhenScoreIsFractional_ReportsTheOffendingPath()
    {
        // A Python float serialises as 78.0 and will not parse into an int.
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            ValidBody.Replace("\"score\": 78", "\"score\": 78.5")));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.AnalyzeNutritionAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains("score", exception.ValidationReason);
    }

    [Fact]
    public async Task AnalyzeNutritionAsync_WhenGradeIsUnknown_ReportsGrade()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            ValidBody.Replace("\"B\"", "\"good\"")));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.AnalyzeNutritionAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains("grade", exception.ValidationReason);
    }

    [Fact]
    public async Task AnalyzeNutritionAsync_WhenScoreOutOfRange_ReportsScore()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            ValidBody.Replace("\"score\": 78", "\"score\": 780")));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.AnalyzeNutritionAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Equal("score is outside 0-100", exception.ValidationReason);
    }

    [Fact]
    public async Task AnalyzeNutritionAsync_WhenAdviceIsNotAnArray_ReportsTheOffendingPath()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            ValidBody.Replace("[\"Add a small evening meal.\"]", "\"Add a small evening meal.\"")));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.AnalyzeNutritionAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains("advice", exception.ValidationReason);
    }

    /// <summary>
    /// Required properties are enforced by the serializer, so a missing field
    /// is reported by name before the field-level validator runs.
    /// </summary>
    [Fact]
    public async Task AnalyzeNutritionAsync_WhenDisclaimerIsMissing_ReportsDisclaimer()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "grade": "B",
              "score": 78,
              "summary": "Slightly under the calorie target.",
              "advice": []
            }
            """));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.AnalyzeNutritionAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains("disclaimer", exception.ValidationReason);
    }

    [Fact]
    public async Task AnalyzeNutritionAsync_WhenBodyIsEmpty_ReportsEmptyBody()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "null"));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.AnalyzeNutritionAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Equal("body was null or empty", exception.ValidationReason);
    }

    [Fact]
    public async Task AnalyzeNutritionAsync_Maps503ToUnavailableNotInvalid()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.ServiceUnavailable,
            """{"code":"service_overloaded","message":"busy","retryable":true,"retryAfterSeconds":20}"""));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            client.AnalyzeNutritionAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Equal("service_overloaded", exception.Code);
        Assert.Equal(20, exception.RetryAfterSeconds);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://classifier.example/Prod/")
        };
        client.DefaultRequestHeaders.Add("X-API-Key", "test-key");
        return client;
    }

    private static ClassifierNutritionRequest CreateRequest() => new()
    {
        Date = "2026-07-15",
        MealCount = 2,
        TotalCalories = 480,
        ScheduledFeedings = 3,
        PetType = ClassifierPetType.Dog
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("X-API-Key", out var values)
                ? values.FirstOrDefault()
                : null;
            return Task.FromResult(responseFactory(request));
        }
    }
}
