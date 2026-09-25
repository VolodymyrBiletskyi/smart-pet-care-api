using smart_pet_care_api.Common.Api;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using smart_pet_care_api.Infrastructure.Email;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.Infrastructure;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.AuthModule.OAuth;
using smart_pet_care_api.Modules.AuthModule.Repository;

namespace smart_pet_care_api.Modules.AuthModule
{
    public static class AuthModuleExtensions
    {
        public static IServiceCollection AddAuthModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // options
            services.Configure<JwtOptions>(configuration.GetSection("JwtOptions"));

            // services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAuthRepository, AuthRepository>();
            services.AddSingleton<IJwtProvider, JwtProvider>();
            services.AddTransient<AuthMiddleware>();
            services.Configure<GoogleOAuthOptions>(configuration.GetSection("GoogleOAuth"));
            services.AddHttpClient<IGoogleOAuth, GoogleOAuth>();
            services.Configure<EmailOptions>(configuration.GetSection("Email"));
            services.AddScoped<IEmailSender, SmtpEmailSender>();
            services.AddMemoryCache();

            // jwt auth
            var jwtOptions = configuration.GetSection("JwtOptions").Get<JwtOptions>()!;

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
                };

                // Left alone, the bearer scheme answers 401 and 403 with an empty
                // body: the two statuses a client meets most often would be the
                // only ones it cannot read a code from.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async challenge =>
                    {
                        challenge.HandleResponse();
                        await WriteErrorAsync(
                            challenge.Response,
                            StatusCodes.Status401Unauthorized,
                            ErrorCodes.Auth.AuthenticationRequired,
                            "Authentication is required.");
                    },
                    OnForbidden = forbidden => WriteErrorAsync(
                        forbidden.Response,
                        StatusCodes.Status403Forbidden,
                        ErrorCodes.Auth.Forbidden,
                        "This account may not perform that action.")
                };
            });

            return services;
        }

        private static Task WriteErrorAsync(
            HttpResponse response, int statusCode, string code, string message)
        {
            if (response.HasStarted)
                return Task.CompletedTask;

            response.StatusCode = statusCode;
            return response.WriteAsJsonAsync(new ApiErrorResponse
            {
                Code = code,
                Message = message,
                TraceId = response.HttpContext.TraceIdentifier
            });
        }
    }
}