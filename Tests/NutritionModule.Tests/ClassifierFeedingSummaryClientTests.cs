using System.Net;
using System.Text;
using System.Text.Json;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using Xunit;

namespace smart_pet_care_api.Modules.NutritionModule.Tests;

/// <summary>
/// Covers the classifier responses that are reported to clients as 502
/// <c>classifier_invalid_response</c>, and the reason recorded for each.
/// </summary>
public sealed class ClassifierFeedingSummaryClientTests
{
    private const string ValidBody = """
        {
          "results": [
            {
              "petId": "pet-1",
              "status": "UNDER_TARGET",
              "targetCalories": 600,
              "actualCalories": 480,
              "deviationPct": -20
            }
          ],
          "disclaimer": "Not veterinary advice."
        }
        """;

    [Fact]
    public async Task SummarizeFeedingAsync_PostsRelativeFeedingSummaryPathWithApiKey()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, ValidBody));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var response = await client.SummarizeFeedingAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        var result = Assert.Single(response.Results);
        Assert.Equal("pet-1", result.PetId);
        Assert.Equal(ClassifierFeedingStatus.UnderTarget, result.Status);
        Assert.Equal(600m, result.TargetCalories);
        Assert.Equal(-20m, result.DeviationPct);
        Assert.Equal(
            new Uri("https://classifier.example/Prod/feeding-summary"),
            handler.RequestUri);
        Assert.Equal("test-key", handler.ApiKey);
    }

    /// <summary>
    /// The wire shape the classifier documents: a pets array, camel-cased, with
    /// the species as a lower-case string and nulls left out.
    /// </summary>
    [Fact]
    public async Task SummarizeFeedingAsync_SerialisesTheDocumentedRequestShape()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, ValidBody));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        await client.SummarizeFeedingAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(handler.RequestBody!);
        var pet = document.RootElement.GetProperty("pets")[0];

        Assert.Equal("pet-1", pet.GetProperty("petId").GetString());
        Assert.Equal("dog", pet.GetProperty("species").GetString());
        Assert.Equal("Labrador", pet.GetProperty("breed").GetString());
        Assert.Equal(12.4m, pet.GetProperty("weightKg").GetDecimal());
        Assert.Equal(12, pet.GetProperty("ageMonths").GetInt32());

        var product = pet.GetProperty("products")[0];
        Assert.Equal("Chicken kibble", product.GetProperty("name").GetString());
        Assert.Equal(480m, product.GetProperty("calories").GetDecimal());
    }

    [Fact]
    public async Task SummarizeFeedingAsync_OmitsAgeMonthsWhenTheBirthDateIsUnknown()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, ValidBody));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        await client.SummarizeFeedingAsync(
            CreateRequest(ageMonths: null),
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(handler.RequestBody!);
        var pet = document.RootElement.GetProperty("pets")[0];
        Assert.False(pet.TryGetProperty("ageMonths", out _));
    }

    [Fact]
    public async Task SummarizeFeedingAsync_RejectsAnUnknownStatus()
    {
        var body = ValidBody.Replace("UNDER_TARGET", "SLIGHTLY_PECKISH");

        var exception = await AssertInvalidAsync(body);

        Assert.Contains("status", exception.ValidationReason);
    }

    /// <summary>Wire values are upper case; the enum is not matched loosely.</summary>
    [Fact]
    public async Task SummarizeFeedingAsync_RejectsALowercaseStatus()
    {
        var body = ValidBody.Replace("UNDER_TARGET", "under_target");

        var exception = await AssertInvalidAsync(body);

        Assert.Contains("status", exception.ValidationReason);
    }

    [Fact]
    public async Task SummarizeFeedingAsync_WhenResultsAreNull_ReportsResults()
    {
        var exception = await AssertInvalidAsync(
            """{ "results": null, "disclaimer": "Not veterinary advice." }""");

        Assert.Equal("results is missing", exception.ValidationReason);
    }

    [Fact]
    public async Task SummarizeFeedingAsync_WhenResultsAreEmpty_ReportsResults()
    {
        var exception = await AssertInvalidAsync(
            """{ "results": [], "disclaimer": "Not veterinary advice." }""");

        Assert.Equal("results is empty", exception.ValidationReason);
    }

    [Fact]
    public async Task SummarizeFeedingAsync_WhenDisclaimerIsNull_ReportsDisclaimer()
    {
        var exception = await AssertInvalidAsync(ValidBody.Replace(
            "\"Not veterinary advice.\"", "null"));

        Assert.Equal("disclaimer is missing", exception.ValidationReason);
    }

    /// <summary>
    /// Required properties are enforced by the serializer, so an absent field is
    /// reported by name before the field-level validator runs.
    /// </summary>
    [Theory]
    [InlineData("\"petId\": \"pet-1\",", "petId")]
    [InlineData("\"targetCalories\": 600,", "targetCalories")]
    public async Task SummarizeFeedingAsync_WhenARequiredResultFieldIsAbsent_ReportsIt(
        string omitted, string expected)
    {
        var exception = await AssertInvalidAsync(ValidBody.Replace(omitted, string.Empty));

        Assert.Contains(expected, exception.ValidationReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SummarizeFeedingAsync_WhenDisclaimerIsAbsent_ReportsDisclaimer()
    {
        var body = """
            {
              "results": [
                {
                  "petId": "pet-1",
                  "status": "ON_TARGET",
                  "targetCalories": 600,
                  "actualCalories": 600,
                  "deviationPct": 0
                }
              ]
            }
            """;

        var exception = await AssertInvalidAsync(body);

        Assert.Contains("disclaimer", exception.ValidationReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SummarizeFeedingAsync_WhenResultsIsNotAnArray_ReportsTheOffendingPath()
    {
        var body = """{ "results": {}, "disclaimer": "Not veterinary advice." }""";

        var exception = await AssertInvalidAsync(body);

        Assert.Contains("$.results", exception.ValidationReason);
    }

    [Fact]
    public async Task SummarizeFeedingAsync_WhenBodyIsEmpty_ReportsEmptyBody()
    {
        var exception = await AssertInvalidAsync("null");

        Assert.Equal("body was null or empty", exception.ValidationReason);
    }

    /// <summary>422 means the request was rejected, not that the service is down.</summary>
    [Fact]
    public async Task SummarizeFeedingAsync_Maps422ToInvalidNotUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.UnprocessableEntity,
            """{ "detail": [{ "loc": ["body", "pets", 0, "weightKg"], "msg": "field required", "type": "missing" }] }"""));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.SummarizeFeedingAsync(CreateRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SummarizeFeedingAsync_Maps503ToUnavailableNotInvalid()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.ServiceUnavailable,
            """{"code":"service_overloaded","message":"busy","retryable":true,"retryAfterSeconds":20}"""));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            client.SummarizeFeedingAsync(CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal("service_overloaded", exception.Code);
        Assert.Equal(20, exception.RetryAfterSeconds);
    }

    private static async Task<ClassifierInvalidResponseException> AssertInvalidAsync(string body)
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, body));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        return await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.SummarizeFeedingAsync(CreateRequest(), TestContext.Current.CancellationToken));
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

    private static ClassifierFeedingSummaryRequest CreateRequest(int? ageMonths = 12) => new()
    {
        Pets =
        [
            new ClassifierFeedingSummaryPet
            {
                PetId = "pet-1",
                Species = ClassifierPetType.Dog,
                Breed = "Labrador",
                WeightKg = 12.4m,
                AgeMonths = ageMonths,
                Products = [new ClassifierFeedingProduct { Name = "Chicken kibble", Calories = 480m }]
            }
        ]
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
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("X-API-Key", out var values)
                ? values.FirstOrDefault()
                : null;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request);
        }
    }
}
