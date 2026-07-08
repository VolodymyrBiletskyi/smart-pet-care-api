using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using smart_pet_care_api.Common.Patching;

namespace smart_pet_care_api.Extensions
{
    public static class ScalarConfig
    {
        public static IServiceCollection AddScalarConfig(this IServiceCollection services)
        {
            services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            services.AddOpenApi(options =>
            {
                // PatchField<T> is deserialized from a plain JSON value by
                // PatchFieldJsonConverterFactory, so its schema must be the inner
                // type's schema, not the struct's {isSet, value} shape.
                options.AddSchemaTransformer(async (schema, context, cancellationToken) =>
                {
                    var type = context.JsonTypeInfo.Type;
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(PatchField<>))
                        return;

                    var valueType = type.GetGenericArguments()[0];
                    var underlying = Nullable.GetUnderlyingType(valueType);
                    var allowsNull = underlying is not null || !valueType.IsValueType;

                    var valueSchema = await context.GetOrCreateSchemaAsync(underlying ?? valueType, cancellationToken: cancellationToken);

                    schema.Type = valueSchema.Type;
                    schema.Format = valueSchema.Format;
                    schema.Items = valueSchema.Items;
                    schema.Enum = valueSchema.Enum;
                    schema.Properties = valueSchema.Properties;
                    schema.Description = "Optional: omit to leave unchanged; null clears the value.";

                    if (allowsNull)
                        schema.Type |= JsonSchemaType.Null;
                });

                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info = new OpenApiInfo
                    {
                        Title = "Smart Pet Care API",
                        Version = "v1"
                    };

                    var env = context.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                    var configuration = context.ApplicationServices.GetRequiredService<IConfiguration>();
                    var apiPublicUrl = configuration["ApiPublicUrl"]
                        ?? (env.IsProduction()
                            ? "https://smart-pet-care.duckdns.org"
                            : "http://localhost:8080");

                    var servers = new List<OpenApiServer>
                    {
                        new OpenApiServer { Url = "https://smart-pet-care.duckdns.org" },
                        new OpenApiServer { Url = "http://localhost:8080" }
                    };

                    if (!env.IsProduction())
                        servers.Reverse();

                    document.Servers = servers;


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
                    PreferredSecuritySchemes = ["Bearer"]
                };
            });

            return app;
        }
    }
}
