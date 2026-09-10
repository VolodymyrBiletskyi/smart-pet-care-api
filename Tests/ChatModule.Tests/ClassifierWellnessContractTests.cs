using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ClassifierWellnessContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Request_SerializesCurrentPythonContract()
    {
        var request = new ClassifierWellnessRequest
        {
            Pet = new ClassifierWellnessPet
            {
                Species = "dog",
                Breed = "Labrador",
                AgeMonths = 36,
                Sex = "male",
                WeightKg = 28.5m
            },
            Activity = new ClassifierWellnessActivity
            {
                AvgStepsPerDay = 8200,
                DaysTracked = 25
            },
            EvaluationWindow = new ClassifierWellnessEvaluationWindow
            {
                StartDate = new DateOnly(2026, 7, 26),
                EndDate = new DateOnly(2026, 8, 24)
            },
            RoutineCare =
            [
                new ClassifierWellnessRoutineCareEntry
                {
                    Type = ClassifierWellnessReminderType.Bathing,
                    LastDoneAt = new DateOnly(2026, 8, 10)
                }
            ]
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request, SerializerOptions));
        var root = document.RootElement;
        Assert.Equal("dog", root.GetProperty("pet").GetProperty("species").GetString());
        Assert.Equal(25, root.GetProperty("activity").GetProperty("daysTracked").GetInt32());
        Assert.Equal("2026-07-26", root.GetProperty("evaluationWindow").GetProperty("startDate").GetString());
        Assert.Equal(0, root.GetProperty("activeConditions").GetArrayLength());
        Assert.Equal("Bathing", root.GetProperty("routineCare")[0].GetProperty("type").GetString());
        Assert.Equal("2026-08-10", root.GetProperty("routineCare")[0].GetProperty("lastDoneAt").GetString());
        Assert.False(root.TryGetProperty("currentSymptoms", out _));
    }

    [Fact]
    public void Response_DeserializesExactPythonEnumsAndNullableScore()
    {
        var response = JsonSerializer.Deserialize<ClassifierWellnessResponse>(
            ValidResponseJson(scoreStatus: "INSUFFICIENT_DATA", scoreFields: ""),
            SerializerOptions);

        Assert.NotNull(response);
        Assert.Null(response.WellnessScore);
        Assert.Equal(ClassifierWellnessScoreStatus.InsufficientData, response.ScoreStatus);
        Assert.Equal(ClassifierWellnessDimensionAvailability.Missing, response.Breakdown.Activity.Availability);
        Assert.Equal(ClassifierWellnessReasonCode.ActivityDataMissing, response.Breakdown.Activity.ReasonCodes[0]);
    }

    [Fact]
    public void Response_DeserializesUpdatedRoutineCareEnums()
    {
        var json = ValidResponseJson(scoreStatus: "INSUFFICIENT_DATA", scoreFields: "")
            .Replace(
                "\"trackingRecommendations\":[]",
                "\"trackingRecommendations\":[{\"dimension\":\"RoutineCare\",\"text\":\"Track grooming.\",\"requiredInputs\":[\"routineCare\"],\"suggestedReminderTypes\":[\"Bathing\",\"TeethCleaning\"]}]");

        var response = JsonSerializer.Deserialize<ClassifierWellnessResponse>(json, SerializerOptions);

        var recommendation = Assert.Single(response!.TrackingRecommendations);
        Assert.Equal(ClassifierWellnessDimension.RoutineCare, recommendation.Dimension);
        Assert.Equal(
            [ClassifierWellnessReminderType.Bathing, ClassifierWellnessReminderType.TeethCleaning],
            recommendation.SuggestedReminderTypes);
    }

    [Fact]
    public async Task Client_PostsWellnessAndReturnsValidatedResponse()
    {
        var handler = new StubHandler(ValidResponseJson(
            "COMPLETE",
            "\"wellnessScore\":82,\"band\":\"EXCELLENT\",\"bandLabel\":\"Excellent\",\"trend\":\"IMPROVING\","));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://classifier.example/Prod/")
        };
        httpClient.DefaultRequestHeaders.Add("X-API-Key", "test-key");

        var result = await new ClassifierClient(httpClient).CalculateWellnessAsync(
            new ClassifierWellnessRequest
            {
                Pet = new ClassifierWellnessPet { Species = "dog" }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(82, result.WellnessScore);
        Assert.Equal(new Uri("https://classifier.example/Prod/wellness"), handler.RequestUri);
        Assert.Equal("test-key", handler.ApiKey);
    }

    [Fact]
    public async Task Client_RejectsScoredResponseWithoutScore()
    {
        using var httpClient = new HttpClient(new StubHandler(ValidResponseJson("COMPLETE", "")))
        {
            BaseAddress = new Uri("https://classifier.example/")
        };

        await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            new ClassifierClient(httpClient).CalculateWellnessAsync(
                new ClassifierWellnessRequest
                {
                    Pet = new ClassifierWellnessPet { Species = "dog" }
                },
                TestContext.Current.CancellationToken));
    }

    private static string ValidResponseJson(string scoreStatus, string scoreFields) => $$"""
        {
          {{scoreFields}}
          "scoreStatus":"{{scoreStatus}}",
          "dataCoverage":0.5,
          "calculationVersion":"1.0.0",
          "evaluatedAt":"2026-08-24T12:00:00Z",
          "breakdown":{
            "activity":{"score":0,"maxScore":20,"availability":"MISSING","included":false,"reasonCodes":["ACTIVITY_DATA_MISSING"]},
            "sleep":{"score":0,"maxScore":15,"availability":"MISSING","included":false,"reasonCodes":["SLEEP_DATA_MISSING"]},
            "diet":{"score":0,"maxScore":20,"availability":"MISSING","included":false,"reasonCodes":["DIET_DATA_MISSING"]},
            "symptoms":{"score":0,"maxScore":15,"availability":"MISSING","included":false,"reasonCodes":["SYMPTOMS_NOT_REPORTED"]},
            "preventiveCare":{"score":0,"maxScore":15,"availability":"MISSING","included":false,"reasonCodes":["PREVENTIVE_CARE_DATA_MISSING"]},
            "baseline":{"score":0,"maxScore":15,"availability":"MISSING","included":false,"reasonCodes":["BASELINE_DATA_MISSING"]}
          },
          "narrative":"Track more data.",
          "recommendations":[],
          "reminders":[],
          "trackingRecommendations":[],
          "disclaimer":"Not veterinary advice."
        }
        """;

    private sealed class StubHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("X-API-Key", out var values)
                ? values.Single()
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
