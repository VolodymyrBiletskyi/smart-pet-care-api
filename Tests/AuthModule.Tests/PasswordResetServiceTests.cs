using Microsoft.AspNetCore.Http;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

public class PasswordResetServiceTests
{
    private const string Email = "owner@example.com";
    private const string OldPassword = "Old-pass1!";
    private const string NewPassword = "New-pass1!";

    // ---- forgot-password -------------------------------------------------

    [Fact]
    public async Task ForgotPassword_StoresOnlyTheHashAndMailsTheRawToken()
    {
        var (authRepo, userRepo, user) = Fixture();
        var mail = new FakeEmailSender();

        await AuthServiceFactory.Create(authRepo, userRepo, mail).ForgotPasswordAsync(Email);

        var stored = Assert.Single(authRepo.ResetTokens);
        Assert.Equal(user.Id, stored.UserId);
        Assert.True(stored.IsActive);

        var sent = Assert.Single(mail.Sent);
        Assert.Equal(Email, sent.To);
        Assert.NotNull(sent.Html);

        // The link carries the raw token; the table only ever sees its hash.
        var rawToken = RawTokenFrom(sent);
        Assert.NotEmpty(rawToken);
        Assert.DoesNotContain(rawToken, stored.TokenHash);
        Assert.Equal(new FakeJwtProvider().HashToken(rawToken), stored.TokenHash);
    }

    /// <summary>
    /// The token travels as a query parameter through a mail client, so it may not
    /// contain anything a URL would have to escape.
    /// </summary>
    [Fact]
    public async Task ForgotPassword_IssuesAUrlSafeToken()
    {
        var (authRepo, userRepo, _) = Fixture();
        var mail = new FakeEmailSender();

        await AuthServiceFactory.Create(authRepo, userRepo, mail).ForgotPasswordAsync(Email);

        var rawToken = RawTokenFrom(mail.Sent[0]);
        Assert.Matches("^[A-Za-z0-9_-]+$", rawToken);
    }

    [Fact]
    public async Task ForgotPassword_NormalisesTheAddress()
    {
        var (authRepo, userRepo, _) = Fixture();
        var mail = new FakeEmailSender();

        await AuthServiceFactory.Create(authRepo, userRepo, mail)
            .ForgotPasswordAsync("  OWNER@Example.COM  ");

        Assert.Single(authRepo.ResetTokens);
        Assert.Single(mail.Sent);
    }

    /// <summary>
    /// The one endpoint where a 404 would be a "does this address have an account"
    /// lookup, so an unknown address is accepted and quietly does nothing.
    /// </summary>
    [Fact]
    public async Task ForgotPassword_SaysNothingAboutAnUnknownAddress()
    {
        var (authRepo, userRepo, _) = Fixture();
        var mail = new FakeEmailSender();

        await AuthServiceFactory.Create(authRepo, userRepo, mail)
            .ForgotPasswordAsync("stranger@example.com");

        Assert.Empty(authRepo.ResetTokens);
        Assert.Empty(mail.Sent);
    }

    /// <summary>
    /// Same reasoning for the cooldown: raising "too soon" would answer the question
    /// the case above refuses to, so the second request is accepted and sends nothing.
    /// </summary>
    [Fact]
    public async Task ForgotPassword_InsideTheCooldownSendsNothingAndDoesNotThrow()
    {
        var (authRepo, userRepo, user) = Fixture();
        authRepo.ResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = "existing",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow.AddSeconds(-5)
        });
        var mail = new FakeEmailSender();

        await AuthServiceFactory.Create(authRepo, userRepo, mail).ForgotPasswordAsync(Email);

        Assert.Single(authRepo.ResetTokens);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task ForgotPassword_PastTheCooldownSupersedesTheEarlierLink()
    {
        var (authRepo, userRepo, user) = Fixture();
        var earlier = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = "earlier",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        authRepo.ResetTokens.Add(earlier);

        await AuthServiceFactory.Create(authRepo, userRepo).ForgotPasswordAsync(Email);

        Assert.NotNull(earlier.UsedAt);
        Assert.Equal(2, authRepo.ResetTokens.Count);
        Assert.Single(authRepo.ResetTokens, t => t.IsActive);
    }

    [Fact]
    public async Task ForgotPassword_LinkLifetimeComesFromTheOptions()
    {
        var (authRepo, userRepo, _) = Fixture();
        var mail = new FakeEmailSender();

        await AuthServiceFactory.Create(
                authRepo, userRepo, mail,
                resetOptions: new PasswordResetOptions { LinkLifetimeMinutes = 90 })
            .ForgotPasswordAsync(Email);

        var stored = authRepo.ResetTokens[0];
        Assert.InRange(
            stored.ExpiresAt,
            DateTime.UtcNow.AddMinutes(89),
            DateTime.UtcNow.AddMinutes(91));

        // And the same number is what the email promises.
        Assert.Contains("90", mail.Sent[0].Text);
    }

    /// <summary>
    /// A Google-only account has no password hash. Blocking the reset there would
    /// leave a user whose Google account is gone with no way in, and it buys nothing:
    /// anyone holding the mailbox can already reset through Google itself.
    /// </summary>
    [Fact]
    public async Task ForgotPassword_WorksForAnAccountThatHasNoPasswordYet()
    {
        var (authRepo, userRepo, user) = Fixture();
        user.PasswordHash = null;
        var mail = new FakeEmailSender();

        await AuthServiceFactory.Create(authRepo, userRepo, mail).ForgotPasswordAsync(Email);

        Assert.Single(authRepo.ResetTokens);
        Assert.Single(mail.Sent);
    }

    // ---- reset-password --------------------------------------------------

    [Fact]
    public async Task ResetPassword_SetsTheNewPasswordAndSpendsTheToken()
    {
        var (authRepo, userRepo, user) = Fixture();
        var rawToken = await IssueLink(authRepo, userRepo);

        await AuthServiceFactory.Create(authRepo, userRepo).ResetPasswordAsync(Request(rawToken));

        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(NewPassword, user.PasswordHash));
        Assert.NotNull(user.UpdatedAt);
        Assert.NotNull(authRepo.ResetTokens[0].UsedAt);
    }

    [Fact]
    public async Task ResetPassword_RevokesEveryLiveSession()
    {
        var (authRepo, userRepo, user) = Fixture();

        // More than GetActiveByUserAsync's ten, which is why the reset path uses the
        // uncapped query: the eleventh would otherwise keep working for seven days.
        for (var i = 0; i < 12; i++)
            authRepo.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = $"hash-{i}",
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

        var rawToken = await IssueLink(authRepo, userRepo);

        await AuthServiceFactory.Create(authRepo, userRepo).ResetPasswordAsync(Request(rawToken));

        Assert.All(authRepo.RefreshTokens, token => Assert.NotNull(token.RevokedAt));
    }

    /// <summary>
    /// Revoking the sessions does not cover this: who receives a notification is decided
    /// by the account a DeviceTokens row belongs to and by no session at all, so a device
    /// that registered while somebody else held the password would keep receiving the
    /// owner's reminders — pet name and reminder text — long after the reset.
    /// </summary>
    [Fact]
    public async Task ResetPassword_DropsEveryPushRegistration()
    {
        var (authRepo, userRepo, user) = Fixture();
        var deviceTokens = new FakeDeviceTokenService();
        var rawToken = await IssueLink(authRepo, userRepo);

        await AuthServiceFactory.Create(authRepo, userRepo, deviceTokens: deviceTokens)
            .ResetPasswordAsync(Request(rawToken));

        Assert.Equal([user.Id], deviceTokens.UnregisteredAll);
    }

    /// <summary>
    /// Only on success. A rejected link must not silence the owner's notifications —
    /// that would make an unusable link a way to turn somebody's pushes off.
    /// </summary>
    [Fact]
    public async Task ResetPassword_KeepsThePushRegistrationsWhenTheLinkIsRejected()
    {
        var (authRepo, userRepo, _) = Fixture();
        var deviceTokens = new FakeDeviceTokenService();
        var rawToken = await IssueLink(authRepo, userRepo);
        authRepo.ResetTokens[0].ExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        await Assert.ThrowsAsync<GoneException>(() =>
            AuthServiceFactory.Create(authRepo, userRepo, deviceTokens: deviceTokens)
                .ResetPasswordAsync(Request(rawToken)));

        Assert.Empty(deviceTokens.UnregisteredAll);
    }

    [Fact]
    public async Task ResetPassword_LeavesAnotherAccountsSessionsAlone()
    {
        var (authRepo, userRepo, _) = Fixture();
        var other = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            TokenHash = "someone-else",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        authRepo.RefreshTokens.Add(other);

        var rawToken = await IssueLink(authRepo, userRepo);

        await AuthServiceFactory.Create(authRepo, userRepo).ResetPasswordAsync(Request(rawToken));

        Assert.Null(other.RevokedAt);
    }

    /// <summary>
    /// Following the link proves control of the mailbox, which is what the six-digit
    /// code proves. Without this an unconfirmed account could reset its password and
    /// still be unable to log in.
    /// </summary>
    [Fact]
    public async Task ResetPassword_ConfirmsAnAddressThatNeverWasConfirmed()
    {
        var (authRepo, userRepo, user) = Fixture();
        user.EmailConfirmed = false;
        user.EmailConfirmedAt = null;

        var rawToken = await IssueLink(authRepo, userRepo);

        await AuthServiceFactory.Create(authRepo, userRepo).ResetPasswordAsync(Request(rawToken));

        Assert.True(user.EmailConfirmed);
        Assert.NotNull(user.EmailConfirmedAt);
    }

    [Fact]
    public async Task ResetPassword_KeepsTheOriginalConfirmationTimestamp()
    {
        var (authRepo, userRepo, user) = Fixture();
        var confirmedAt = DateTime.UtcNow.AddDays(-30);
        user.EmailConfirmedAt = confirmedAt;

        var rawToken = await IssueLink(authRepo, userRepo);

        await AuthServiceFactory.Create(authRepo, userRepo).ResetPasswordAsync(Request(rawToken));

        Assert.Equal(confirmedAt, user.EmailConfirmedAt);
    }

    /// <summary>
    /// Base64url has no whitespace, so a token that arrives with some picked it up on
    /// the way out of the deep link rather than being a different token.
    /// </summary>
    [Fact]
    public async Task ResetPassword_AcceptsATokenWithWhitespaceAroundIt()
    {
        var (authRepo, userRepo, user) = Fixture();
        var rawToken = await IssueLink(authRepo, userRepo);

        await AuthServiceFactory.Create(authRepo, userRepo)
            .ResetPasswordAsync(Request($" {rawToken}\n"));

        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(NewPassword, user.PasswordHash));
    }

    [Fact]
    public async Task ResetPassword_RejectsAnUnknownToken()
    {
        var (authRepo, userRepo, user) = Fixture();
        await IssueLink(authRepo, userRepo);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            AuthServiceFactory.Create(authRepo, userRepo).ResetPasswordAsync(Request("not-a-token")));

        Assert.Equal(ErrorCodes.Auth.ResetTokenInvalid, error.Code);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(OldPassword, user.PasswordHash));
    }

    [Fact]
    public async Task ResetPassword_RejectsTheSameTokenTwice()
    {
        var (authRepo, userRepo, _) = Fixture();
        var rawToken = await IssueLink(authRepo, userRepo);
        var service = AuthServiceFactory.Create(authRepo, userRepo);

        await service.ResetPasswordAsync(Request(rawToken));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(Request(rawToken)));

        Assert.Equal(ErrorCodes.Auth.ResetTokenInvalid, error.Code);
    }

    [Fact]
    public async Task ResetPassword_RejectsAnExpiredTokenAsGone()
    {
        var (authRepo, userRepo, user) = Fixture();
        var rawToken = await IssueLink(authRepo, userRepo);
        authRepo.ResetTokens[0].ExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        var error = await Assert.ThrowsAsync<GoneException>(() =>
            AuthServiceFactory.Create(authRepo, userRepo).ResetPasswordAsync(Request(rawToken)));

        Assert.Equal(ErrorCodes.Auth.ResetTokenExpired, error.Code);
        Assert.Equal(StatusCodes.Status410Gone, error.StatusCode);
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(OldPassword, user.PasswordHash));
    }

    /// <summary>
    /// A superseded link is spent, not expired: it is still inside its lifetime, and
    /// answering 410 would tell the user to wait rather than to use the newer mail.
    /// </summary>
    [Fact]
    public async Task ResetPassword_RejectsASupersededLinkAsInvalid()
    {
        var (authRepo, userRepo, _) = Fixture();
        var first = await IssueLink(authRepo, userRepo);
        authRepo.ResetTokens[0].CreatedAt = DateTime.UtcNow.AddMinutes(-5);
        await AuthServiceFactory.Create(authRepo, userRepo).ForgotPasswordAsync(Email);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            AuthServiceFactory.Create(authRepo, userRepo).ResetPasswordAsync(Request(first)));

        Assert.Equal(ErrorCodes.Auth.ResetTokenInvalid, error.Code);
    }

    [Fact]
    public async Task ResetPassword_LetsTheUserLogInWithTheNewPassword()
    {
        var (authRepo, userRepo, _) = Fixture();
        var rawToken = await IssueLink(authRepo, userRepo);
        var service = AuthServiceFactory.Create(authRepo, userRepo);

        await service.ResetPasswordAsync(Request(rawToken));

        var pair = await service.LoginAsync(new LoginRequest { Email = Email, Password = NewPassword });
        Assert.NotNull(pair.RefreshToken);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest { Email = Email, Password = OldPassword }));
    }

    // ---- the landing page's check ----------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    public async Task IsPasswordResetTokenUsable_IsFalseForAnythingUnknown(string token)
    {
        var (authRepo, userRepo, _) = Fixture();
        await IssueLink(authRepo, userRepo);

        Assert.False(await AuthServiceFactory.Create(authRepo, userRepo)
            .IsPasswordResetTokenUsableAsync(token));
    }

    /// <summary>
    /// The page is also what a mail scanner prefetches, so the check must not spend
    /// the token it is checking.
    /// </summary>
    [Fact]
    public async Task IsPasswordResetTokenUsable_DoesNotSpendTheToken()
    {
        var (authRepo, userRepo, _) = Fixture();
        var rawToken = await IssueLink(authRepo, userRepo);
        var service = AuthServiceFactory.Create(authRepo, userRepo);

        Assert.True(await service.IsPasswordResetTokenUsableAsync(rawToken));
        Assert.True(await service.IsPasswordResetTokenUsableAsync(rawToken));
        Assert.Null(authRepo.ResetTokens[0].UsedAt);

        await service.ResetPasswordAsync(Request(rawToken));
        Assert.False(await service.IsPasswordResetTokenUsableAsync(rawToken));
    }

    [Fact]
    public async Task IsPasswordResetTokenUsable_IsFalseOnceExpired()
    {
        var (authRepo, userRepo, _) = Fixture();
        var rawToken = await IssueLink(authRepo, userRepo);
        authRepo.ResetTokens[0].ExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        Assert.False(await AuthServiceFactory.Create(authRepo, userRepo)
            .IsPasswordResetTokenUsableAsync(rawToken));
    }

    // ---- helpers ---------------------------------------------------------

    private static (FakeAuthRepository, FakeUserRepository, User) Fixture()
    {
        var user = new User
        {
            Email = Email,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(OldPassword),
            EmailConfirmed = true,
            EmailConfirmedAt = DateTime.UtcNow.AddDays(-30)
        };

        var userRepo = new FakeUserRepository();
        userRepo.Users.Add(user);

        return (new FakeAuthRepository(), userRepo, user);
    }

    /// <summary>Runs the real forgot-password path and returns the token it mailed.</summary>
    private static async Task<string> IssueLink(
        FakeAuthRepository authRepo, FakeUserRepository userRepo)
    {
        var mail = new FakeEmailSender();
        await AuthServiceFactory.Create(authRepo, userRepo, mail).ForgotPasswordAsync(Email);
        return RawTokenFrom(mail.Sent[0]);
    }

    private static string RawTokenFrom(SentEmail email)
    {
        var index = email.Text.IndexOf(StubResetLinkBuilder.Base, StringComparison.Ordinal);
        Assert.True(index >= 0, "the email does not carry a reset link");

        return email.Text[(index + StubResetLinkBuilder.Base.Length)..]
            .Split(' ', '\r', '\n')[0];
    }

    private static ResetPasswordRequest Request(string token) => new()
    {
        Token = token,
        Password = NewPassword,
        PasswordConfirm = NewPassword
    };
}
