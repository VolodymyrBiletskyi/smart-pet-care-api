using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace smart_pet_care_api.Extensions
{
    public static class ScalarConfig
    {
        // This is where we configure Scalar to work with our API, including setting up JWT authentication and customizing the OpenAPI document.
        public static IServiceCollection AddScalarConfig(this IServiceCollection services)
        {
            services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info = new OpenApiInfo
                    {
                        Title = "Smart Pet Care API",
                        Version = "v1"
                    };

                    var env = context.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

                    document.Servers = new List<OpenApiServer>
                    {
                        new OpenApiServer
                        {
                            Url = env.IsProduction()
                                ? "https://smart-pet-care.duckdns.org"
                                : "http://localhost:8080"
                        }
                    };
                    // Ensure the Components and SecuritySchemes dictionaries are initialized before adding our Bearer scheme

                    document.Components ??= new OpenApiComponents();

                    if (document.Components.SecuritySchemes == null)
                        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();

                    document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Enter your JWT token"
                    });

                    return Task.CompletedTask;
                });
            });

            return services;
        }
        // This extension method is used in Program.cs to add the Scalar API reference and OpenAPI document to our application.
        public static IApplicationBuilder UseScalarConfig(this WebApplication app)
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.Title = "Smart Pet Care API";
                options.Theme = ScalarTheme.DeepSpace;
                options.Authentication = new ScalarAuthenticationOptions
                {
                    PreferredSecuritySchemes = ["Bearer"]
                };
            });

            return app;
        }
    }
}