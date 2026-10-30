using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Infrastructure.Email;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.AuthModule.OAuth;
using smart_pet_care_api.Modules.AuthModule.Repository;
using smart_pet_care_api.Modules.NotificationModule.Domain;
using smart_pet_care_api.Modules.NotificationModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

/// <summary>
/// In-memory stand-ins for everything <see cref="AuthService"/> depends on. The
/// reset paths read and write across two repositories that share one DbContext in
/// production, so both fakes are backed by plain lists and one SaveChanges counter.
/// </summary>
internal sealed class FakeAuthRepository : IAuthRepository
{
    public List<RefreshToken> RefreshTokens { get; } = [];
    public List<EmailConfirmationCode> ConfirmationCodes { get; } = [];
    public List<PasswordResetToken> ResetTokens { get; } = [];
    public int SaveChangesCalls { get; private set; }

    public Task AddAsync(RefreshToken token)
    {
        RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<List<RefreshToken>> GetActiveByUserAsync(Guid userId) =>
        Task.FromResult(Active(userId).Take(10).ToList());

    public Task<List<RefreshToken>> GetAllActiveByUserAsync(Guid userId) =>
        Task.FromResult(Active(userId).ToList());

    private IEnumerable<RefreshToken> Active(Guid userId) =>
        RefreshTokens.Where(t =>
            t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash) =>
        Task.FromResult(RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task AddConfirmationCodeAsync(EmailConfirmationCode code)
    {
        ConfirmationCodes.Add(code);
        return Task.CompletedTask;
    }

    public Task<EmailConfirmationCode?> GetLatestConfirmationCodeAsync(Guid userId) =>
        Task.FromResult(ConfirmationCodes
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault());

    public Task<List<EmailConfirmationCode>> GetActiveConfirmationCodesAsync(Guid userId) =>
        Task.FromResult(ConfirmationCodes.Where(c => c.UserId == userId && c.IsActive).ToList());

    public Task AddPasswordResetTokenAsync(PasswordResetToken token)
    {
        ResetTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<PasswordResetToken?> GetPasswordResetTokenAsync(string tokenHash) =>
        Task.FromResult(ResetTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<PasswordResetToken?> GetLatestPasswordResetTokenAsync(Guid userId) =>
        Task.FromResult(ResetTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault());

    public Task<List<PasswordResetToken>> GetActivePasswordResetTokensAsync(Guid userId) =>
        Task.FromResult(ResetTokens.Where(t => t.UserId == userId && t.IsActive).ToList());

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(0);
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<UserCredentials?> GetCredentialsByEmailAsync(string email) =>
        Task.FromResult(Users
            .Where(u => u.Email == email)
            .Select(u => new UserCredentials(u.Id, u.Email, u.PasswordHash, u.EmailConfirmed))
            .FirstOrDefault());

    public Task<UserCredentials?> GetCredentialsByIdAsync(Guid id) =>
        Task.FromResult(Users
            .Where(u => u.Id == id)
            .Select(u => new UserCredentials(u.Id, u.Email, u.PasswordHash, u.EmailConfirmed))
            .FirstOrDefault());

    public Task<User?> GetTrackedByIdAsync(Guid id) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetTrackedByEmailAsync(string email) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

    public Task<User> AddAsync(User entity)
    {
        Users.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<int> SaveChangesAsync() => Task.FromResult(0);

    public Task<UserProfileRow?> GetProfileByIdAsync(Guid id) =>
        throw new NotSupportedException();

    public Task<UserAvatar?> GetAvatarByIdAsync(Guid id) =>
        throw new NotSupportedException();

    public Task DeleteAsync(Guid id) => throw new NotSupportedException();

    public Task<bool> ExistsAsync(Guid userId) =>
        Task.FromResult(Users.Any(u => u.Id == userId));

    public Task<ExternalLogin?> GetExternalLoginAsync(AuthProvider provider, string providerUserId) =>
        Task.FromResult<ExternalLogin?>(null);

    public Task AddExternalLoginAsync(ExternalLogin login) => Task.CompletedTask;
}

internal sealed record SentEmail(string To, string Subject, string Text, string? Html);

internal sealed class FakeEmailSender : IEmailSender
{
    public List<SentEmail> Sent { get; } = [];

    public Task SendAsync(string toAddress, string subject, string textBody, string? htmlBody = null)
    {
        Sent.Add(new SentEmail(toAddress, subject, textBody, htmlBody));
        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailTemplateProvider : IEmailTemplateProvider
{
    public EmailBody ConfirmationCode(string code, int expiresInMinutes) =>
        new($"text {code} {expiresInMinutes}", $"html {code} {expiresInMinutes}");

    public EmailBody PasswordReset(string link, int expiresInMinutes) =>
        new($"text {link} {expiresInMinutes}", $"html {link} {expiresInMinutes}");
}

/// <summary>Real SHA256, so a hash stored by one call is found by the next.</summary>
internal sealed class FakeJwtProvider : IJwtProvider
{
    public int AccessTokenMinutes => 15;

    public string GenerateAccessToken(Guid userId, string email) => $"access-{userId}";

    public string GenerateRefreshToken() => Guid.NewGuid().ToString();

    public string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal sealed class FakeDeviceTokenService : IDeviceTokenService
{
    public List<(Guid UserId, string Token)> Unregistered { get; } = [];
    public List<Guid> UnregisteredAll { get; } = [];

    public Task RegisterAsync(Guid userId, RegisterDeviceTokenDto dto) => Task.CompletedTask;

    public Task<bool> UnregisterAsync(Guid userId, string token)
    {
        Unregistered.Add((userId, token));
        return Task.FromResult(true);
    }

    public Task<int> UnregisterAllAsync(Guid userId)
    {
        UnregisteredAll.Add(userId);
        return Task.FromResult(1);
    }
}

internal sealed class FakeGoogleOAuth : IGoogleOAuth
{
    public string GetAuthorizationUrl() => "https://accounts.google.test";

    public Task<GoogleUserInfo> ExchangeCodeAsync(string code) => throw new NotSupportedException();

    public Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken) => throw new NotSupportedException();
}

internal sealed class StubResetLinkBuilder : IPasswordResetLinkBuilder
{
    public const string Base = "https://api.test/reset-password?token=";

    public string EmailLink(string rawToken) => Base + rawToken;

    public string AppLink(string rawToken) => "miso://reset-password?token=" + rawToken;
}

internal static class AuthServiceFactory
{
    public static AuthService Create(
        FakeAuthRepository authRepo,
        FakeUserRepository userRepo,
        FakeEmailSender? emailSender = null,
        IPasswordResetLinkBuilder? links = null,
        PasswordResetOptions? resetOptions = null,
        FakeDeviceTokenService? deviceTokens = null) =>
        new(
            new FakeJwtProvider(),
            userRepo,
            authRepo,
            new FakeGoogleOAuth(),
            emailSender ?? new FakeEmailSender(),
            new FakeEmailTemplateProvider(),
            deviceTokens ?? new FakeDeviceTokenService(),
            links ?? new StubResetLinkBuilder(),
            Options.Create(resetOptions ?? new PasswordResetOptions()));
}
