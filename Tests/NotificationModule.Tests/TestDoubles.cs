using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Infrastructure.Email;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.AuthModule.OAuth;
using smart_pet_care_api.Modules.AuthModule.Repository;
using smart_pet_care_api.Modules.NotificationModule.Domain;
using smart_pet_care_api.Modules.NotificationModule.DTOs.Requests;
using smart_pet_care_api.Modules.NotificationModule.Repository;
using smart_pet_care_api.Modules.UserModule.DTOs.Requests;
using smart_pet_care_api.Modules.UserModule.DTOs.Responses;
using smart_pet_care_api.Modules.UserModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NotificationModule.Tests;

internal sealed class FakeDeviceTokenRepository : IDeviceTokenRepository
{
    public List<DeviceToken> Rows { get; } = new();
    public List<DeviceToken> Upserted { get; } = new();
    public List<(Guid UserId, string Token)> ScopedRemovals { get; } = new();
    public List<string> BulkRemovals { get; } = new();
    public DateTime? StaleCutoff { get; private set; }
    public int SaveChangesCalls { get; private set; }

    /// <summary>Every mutation in order, so a test can assert what ran before what.</summary>
    public List<string> Calls { get; } = new();

    public Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId)
    {
        Calls.Add(nameof(GetByUserIdAsync));
        return Task.FromResult<IReadOnlyList<DeviceToken>>(
            Rows.Where(r => r.UserId == userId).ToList());
    }

    public Task UpsertAsync(DeviceToken token)
    {
        Calls.Add(nameof(UpsertAsync));
        Upserted.Add(token);

        // Mirrors the real ON CONFLICT ("Token") target: the token moves to the new
        // owner instead of producing a second row for the same device.
        var existing = Rows.FirstOrDefault(r => r.Token == token.Token);
        if (existing is not null) Rows.Remove(existing);
        Rows.Add(token);

        return Task.CompletedTask;
    }

    public Task<bool> RemoveByUserAndTokenAsync(Guid userId, string token)
    {
        Calls.Add(nameof(RemoveByUserAndTokenAsync));
        ScopedRemovals.Add((userId, token));

        var existing = Rows.FirstOrDefault(r => r.UserId == userId && r.Token == token);
        if (existing is null) return Task.FromResult(false);

        Rows.Remove(existing);
        return Task.FromResult(true);
    }

    public Task RemoveByTokensAsync(IEnumerable<string> tokens)
    {
        Calls.Add(nameof(RemoveByTokensAsync));
        var set = tokens.Distinct().ToList();
        BulkRemovals.AddRange(set);
        Rows.RemoveAll(r => set.Contains(r.Token));
        return Task.CompletedTask;
    }

    public Task<int> RemoveStaleAsync(DateTime olderThan)
    {
        Calls.Add(nameof(RemoveStaleAsync));
        StaleCutoff = olderThan;
        return Task.FromResult(Rows.RemoveAll(r => r.LastSeenAt < olderThan));
    }

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(0);
    }
}

internal sealed class FakeDeviceTokenService : IDeviceTokenService
{
    public List<(Guid UserId, string Token)> Unregistered { get; } = new();
    public bool UnregisterResult { get; set; } = true;

    /// <summary>Stands in for the delete losing its row to the push sender's own pruning.</summary>
    public bool ThrowOnUnregister { get; set; }

    public Task RegisterAsync(Guid userId, RegisterDeviceTokenDto dto) =>
        throw new NotSupportedException();

    public Task<bool> UnregisterAsync(Guid userId, string token)
    {
        Unregistered.Add((userId, token));

        if (ThrowOnUnregister)
            throw new DbUpdateConcurrencyException("the row was deleted concurrently");

        return Task.FromResult(UnregisterResult);
    }
}

internal sealed class FakeAuthRepository : IAuthRepository
{
    public List<RefreshToken> ActiveTokens { get; } = new();
    public int SaveChangesCalls { get; private set; }

    public Task<List<RefreshToken>> GetActiveByUserAsync(Guid userId) =>
        Task.FromResult(ActiveTokens.Where(t => t.UserId == userId).ToList());

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(0);
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash) => throw new NotSupportedException();
    public Task AddAsync(RefreshToken token) => throw new NotSupportedException();
    public Task AddConfirmationCodeAsync(EmailConfirmationCode code) => throw new NotSupportedException();
    public Task<EmailConfirmationCode?> GetLatestConfirmationCodeAsync(Guid userId) => throw new NotSupportedException();
    public Task<List<EmailConfirmationCode>> GetActiveConfirmationCodesAsync(Guid userId) => throw new NotSupportedException();
}

/// <summary>
/// Logout touches neither of these, but AuthService takes them in its constructor.
/// They throw rather than return defaults so a test that starts depending on one fails
/// loudly instead of silently exercising a stub.
/// </summary>
internal sealed class UnusedUserRepository : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id) => throw new NotSupportedException();
    public Task<User?> GetByEmailAsync(string email) => throw new NotSupportedException();
    public Task<User?> GetTrackedByEmailAsync(string email) => throw new NotSupportedException();
    public Task<User> AddAsync(User entity) => throw new NotSupportedException();
    public Task<int> SaveChangesAsync() => throw new NotSupportedException();
    public Task DeleteAsync(Guid id) => throw new NotSupportedException();
    public Task<bool> ExistsAsync(Guid userId) => throw new NotSupportedException();
    public Task<ExternalLogin?> GetExternalLoginAsync(AuthProvider provider, string providerUserId) => throw new NotSupportedException();
    public Task AddExternalLoginAsync(ExternalLogin login) => throw new NotSupportedException();
}

internal sealed class UnusedJwtProvider : IJwtProvider
{
    public int AccessTokenMinutes => throw new NotSupportedException();
    public string GenerateAccessToken(User user) => throw new NotSupportedException();
    public string GenerateRefreshToken() => throw new NotSupportedException();
    public string HashToken(string token) => throw new NotSupportedException();
}

internal sealed class UnusedGoogleOAuth : IGoogleOAuth
{
    public string GetAuthorizationUrl() => throw new NotSupportedException();
    public Task<GoogleUserInfo> ExchangeCodeAsync(string code) => throw new NotSupportedException();
    public Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken) => throw new NotSupportedException();
}

internal sealed class UnusedEmailSender : IEmailSender
{
    public Task SendAsync(string toAddress, string subject, string textBody, string? htmlBody = null) =>
        throw new NotSupportedException();
}

internal sealed class UnusedEmailTemplates : IEmailTemplateProvider
{
    public EmailBody ConfirmationCode(string code, int expiresInMinutes) =>
        throw new NotSupportedException();
}
