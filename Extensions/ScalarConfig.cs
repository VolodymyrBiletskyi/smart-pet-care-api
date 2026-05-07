using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace smart_pet_care_api.Extensions
{
    public static class ScalarConfig
    {
        public static IServiceCollection AddScalarConfig(this IServiceCollection services)
        {
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

        public static IApplicationBuilder UseScalarConfig(this WebApplication app)
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.Title = "Smart Pet Care API";
                options.Theme = ScalarTheme.DeepSpace;
                options.Authentication = new ScalarAuthenticationOptions
                {
                    PreferredSecurityScheme = "Bearer"
                };
            });

            return app;
        }
    }
}