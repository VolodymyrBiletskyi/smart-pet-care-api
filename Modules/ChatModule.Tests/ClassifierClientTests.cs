using System.Net;
using System.Text;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ClassifierClientTests
{
    [Fact]
    public async Task ChatAsync_PostsRelativeChatPathWithApiKey()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "mode": "general",
              "answer": "Answer",
              "symptomSummary": "Summary",
              "prediction": null,
              "relatedTopics": [],
              "needsClarification": false,
              "disclaimer": "Disclaimer"
            }
            """));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var response = await client.ChatAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Answer", response.Answer);
        Assert.Equal(
            new Uri("https://classifier.example/Prod/chat"),
            handler.RequestUri);
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Contains("petType", handler.RequestContent);
        Assert.Contains("dog", handler.RequestContent);
    }

    [Fact]
    public async Task ChatAsync_Maps422ToInvalidResponse()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.UnprocessableEntity,
            """{"detail":[{"msg":"invalid"}]}"""));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(
            () => client.ChatAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.Contains("detail", exception.ResponseContent);
    }

    [Fact]
    public async Task ChatAsync_MapsMalformedBodyToInvalidResponse()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """{"mode":"general"}"""));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            client.ChatAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChatAsync_MapsServerErrorToUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var httpClient = CreateHttpClient(handler);
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierUnavailableException>(
            () => client.ChatAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
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

    private static ClassifierChatRequest CreateRequest()
    {
        return new ClassifierChatRequest
        {
            SessionId = Guid.NewGuid().ToString("D"),
            Messages =
            [
                new ClassifierChatMessage
                {
                    Role = ClassifierChatRole.User,
                    Content = "Hello"
                }
            ],
            PetType = ClassifierPetType.Dog
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

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string RequestContent { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("X-API-Key").Single();
            RequestContent = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
