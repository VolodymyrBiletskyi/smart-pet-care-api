using System.Net;
using System.Text.Json;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ClassifierErrorContractTests
{
    [Fact]
    public void ClassifierErrorResponse_DeserializesPythonContract()
    {
        const string json =
            """
            {
              "code": "rate_limit_exceeded",
              "message": "Classifier rate limit exceeded",
              "retryable": true,
              "retryAfterSeconds": 30
            }
            """;

        var response = JsonSerializer.Deserialize<ClassifierErrorResponse>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(response);
        Assert.Equal("rate_limit_exceeded", response.Code);
        Assert.Equal("Classifier rate limit exceeded", response.Message);
        Assert.True(response.Retryable);
        Assert.Equal(30, response.RetryAfterSeconds);
    }

    [Fact]
    public void ClassifierRateLimitedException_ExposesCodeAndRetryDelay()
    {
        var exception = new ClassifierRateLimitedException(
            "Rate limited",
            "rate_limit_exceeded",
            retryAfterSeconds: 30);

        Assert.Equal("rate_limit_exceeded", exception.Code);
        Assert.Equal(30, exception.RetryAfterSeconds);
    }

    [Fact]
    public void ClassifierUnavailableException_ExposesCodeAndRetryDelay()
    {
        var exception = new ClassifierUnavailableException(
            "Overloaded",
            HttpStatusCode.ServiceUnavailable,
            code: "service_overloaded",
            retryAfterSeconds: 20);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal("service_overloaded", exception.Code);
        Assert.Equal(20, exception.RetryAfterSeconds);
    }
}
