using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class ClassifierOperationalTests
{
    [Fact]
    public async Task ChatAsync_HttpTimeoutMapsToUnavailable()
    {
        using var httpClient = new HttpClient(new BlockingHttpMessageHandler())
        {
            BaseAddress = new Uri("https://classifier.example/"),
            Timeout = TimeSpan.FromMilliseconds(100)
        };
        var client = new ClassifierClient(httpClient);

        var exception = await Assert.ThrowsAsync<ClassifierUnavailableException>(
            () => client.ChatAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

        Assert.Contains(
            "timed out",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatAsync_CallerCancellationIsNotMappedToUnavailable()
    {
        using var httpClient = new HttpClient(new BlockingHttpMessageHandler())
        {
            BaseAddress = new Uri("https://classifier.example/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var client = new ClassifierClient(httpClient);
        using var cancellation = new CancellationTokenSource();

        var call = client.ChatAsync(CreateRequest(), cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public void AddClassifier_ConfiguresTypedHttpClient()
    {
        using var provider = BuildProvider(
            "https://classifier.example/api",
            "secret-key",
            "12");
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        using var client = factory.CreateClient(
            typeof(IClassifierClient).Name);

        Assert.Equal(new Uri("https://classifier.example/api/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(12), client.Timeout);
        Assert.Equal(
            "secret-key",
            client.DefaultRequestHeaders.GetValues("X-API-Key").Single());
    }

    [Fact]
    public void AddClassifier_RejectsInvalidBaseUrl()
    {
        using var provider = BuildProvider("relative/url", "secret-key", "30");

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<ClassifierOptions>>().Value);

        Assert.Contains("BaseUrl", exception.Message);
    }

    [Fact]
    public void AddClassifier_RejectsMissingApiKey()
    {
        using var provider = BuildProvider(
            "https://classifier.example/",
            string.Empty,
            "30");

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<ClassifierOptions>>().Value);

        Assert.Contains("ApiKey", exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    public void AddClassifier_RejectsTimeoutOutsideSupportedRange(string timeout)
    {
        using var provider = BuildProvider(
            "https://classifier.example/",
            "secret-key",
            timeout);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<ClassifierOptions>>().Value);

        Assert.Contains("TimeoutSeconds", exception.Message);
    }

    private static ServiceProvider BuildProvider(
        string baseUrl,
        string apiKey,
        string timeoutSeconds)
    {
        var values = new Dictionary<string, string?>
        {
            ["Classifier:BaseUrl"] = baseUrl,
            ["Classifier:ApiKey"] = apiKey,
            ["Classifier:TimeoutSeconds"] = timeoutSeconds
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddClassifier(configuration);
        return services.BuildServiceProvider();
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

    private sealed class BlockingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The blocking handler completed.");
        }
    }
}
