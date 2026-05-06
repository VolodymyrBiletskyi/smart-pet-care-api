using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
            });

            return services;
        }
    }
}