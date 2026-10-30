using smart_pet_care_api.Common.Api;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using smart_pet_care_api.Infrastructure.Email;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.Infrastructure;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.AuthModule.OAuth;
using smart_pet_care_api.Modules.AuthModule.Repository;

namespace smart_pet_care_api.Modules.AuthModule
{
    /// <summary>
    /// Policy names for the anonymous auth endpoints. Two of them, because the cost of a
    /// request differs: a credential attempt burns BCrypt on this server, while a
    /// registration or a resend spends someone else's inbox and this domain's sending
    /// reputation.
    /// </summary>
    public static class AuthRateLimitPolicies
    {
        public const string Credentials = "auth-credentials";
        public const string Email = "auth-email";
    }

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
            // Singleton: the templates are read from disk once and held.
            services.AddSingleton<IEmailTemplateProvider, EmailTemplateProvider>();
            services.AddSingleton<IPasswordResetPageProvider, PasswordResetPageProvider>();
            services.AddMemoryCache();

            // ApiPublicUrl as the fallback rather than a second variable to keep in step:
            // it is already the one place a deployment states where it answers from.
            services.Configure<PasswordResetOptions>(options =>
            {
                configuration.GetSection("PasswordReset").Bind(options);

                if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
                    options.PublicBaseUrl = configuration["ApiPublicUrl"] ?? string.Empty;

                // An env var that exists but is empty binds "" over the default, which
                // would leave /reset-password handing the browser off to nothing. A host
                // whose .env is missing the variable gets the default instead.
                if (string.IsNullOrWhiteSpace(options.AppLink))
                    options.AppLink = PasswordResetOptions.DefaultAppLink;
            });
            // The link builder falls back to the request's own host when neither is set.
            services.AddHttpContextAccessor();
            services.AddScoped<IPasswordResetLinkBuilder, PasswordResetLinkBuilder>();

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

        /// <summary>
        /// Caps the anonymous auth endpoints per client address. Without it,
        /// <c>login</c> is a free guessing oracle that also burns an enhanced BCrypt
        /// verification per attempt, and <c>register</c> has no cap at all: the 60-second
        /// resend cooldown is per account, so a caller posting fresh addresses never meets
        /// it and the SMTP sender becomes a way to mail strangers.
        /// </summary>
        public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                // Fixed window rather than token bucket: the limit a client has to reason
                // about is "n tries a minute", and Retry-After below can then name the
                // moment the window turns over.
                options.AddPolicy(AuthRateLimitPolicies.Credentials, context =>
                    RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new()
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1)
                    }));

                options.AddPolicy(AuthRateLimitPolicies.Email, context =>
                    RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new()
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10)
                    }));

                // The limiter writes the response itself, so without this a 429 would be
                // the one failure with an empty body -- the same hole the bearer scheme's
                // events above close.
                options.OnRejected = async (rejected, cancellationToken) =>
                {
                    rejected.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        rejected.HttpContext.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                    await WriteErrorAsync(
                        rejected.HttpContext.Response,
                        StatusCodes.Status429TooManyRequests,
                        ErrorCodes.Auth.TooManyRequests,
                        "Too many attempts. Try again shortly.",
                        cancellationToken);
                };
            });

            return services;
        }

        /// <summary>
        /// The client address, which is the real one only because UseForwardedHeaders runs
        /// before the limiter and rewrites RemoteIpAddress from the entry nginx appended.
        /// A caller with no address at all shares one partition rather than escaping the
        /// limit.
        /// </summary>
        private static string ClientKey(HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private static Task WriteErrorAsync(
            HttpResponse response, int statusCode, string code, string message,
            CancellationToken cancellationToken = default)
        {
            if (response.HasStarted)
                return Task.CompletedTask;

            response.StatusCode = statusCode;
            return response.WriteAsJsonAsync(new ApiErrorResponse
            {
                Code = code,
                Message = message,
                TraceId = response.HttpContext.TraceIdentifier
            }, cancellationToken);
        }
    }
}