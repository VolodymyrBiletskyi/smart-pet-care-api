using Microsoft.Extensions.Options;

namespace smart_pet_care_api.Infrastructure.Classifier;

public static class ClassifierServiceCollectionExtensions
{
    private const int MaximumTimeoutSeconds = 2_147_483;

    public static IServiceCollection AddClassifier(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ClassifierOptions>()
            .Bind(configuration.GetSection(ClassifierOptions.SectionName))
            .Validate(
                options => IsValidBaseUrl(options.BaseUrl),
                $"{ClassifierOptions.SectionName}:BaseUrl must be an absolute HTTP or HTTPS URL.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                $"{ClassifierOptions.SectionName}:ApiKey is required.")
            .Validate(
                options => options.TimeoutSeconds is > 0 and <= MaximumTimeoutSeconds,
                $"{ClassifierOptions.SectionName}:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<IClassifierClient, ClassifierClient>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<ClassifierOptions>>()
                .Value;
            var baseUrl = options.BaseUrl.EndsWith('/')
                ? options.BaseUrl
                : $"{options.BaseUrl}/";

            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("X-API-Key", options.ApiKey);
        });

        return services;
    }

    private static bool IsValidBaseUrl(string baseUrl)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
