using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

/// <summary>
/// A completed reset used to notify nobody. The owner's only symptom was a password
/// that stopped working — and on a Google-only account, which never meets a password
/// prompt, there was no symptom at all.
/// </summary>
public class PasswordChangedNoticeTests
{
    private const string Email = "owner@example.com";
    private const string NewPassword = "New-pass1!";

    [Fact]
    public async Task AResetMailsTheNotice()
    {
        var (authRepo, userRepo) = Fixture();
        var mail = new FakeEmailSender();

        await Reset(authRepo, userRepo, mail);

        // The link first, then the notice.
        Assert.Equal(2, mail.Sent.Count);
        var notice = mail.Sent[1];

        Assert.Equal(Email, notice.To);
        Assert.Contains("password was changed", notice.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(notice.Html);
    }

    /// <summary>
    /// The password has already changed by the time the notice is sent, so a dead SMTP
    /// server must not answer the client as though the reset had failed — that would
    /// leave the user holding a password they believe was rejected.
    /// </summary>
    [Fact]
    public async Task AResetSucceedsWhenTheNoticeCannotBeSent()
    {
        var (authRepo, userRepo) = Fixture();
        var mail = new FakeEmailSender { FailOnSubject = "was changed" };

        await Reset(authRepo, userRepo, mail);

        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(NewPassword, userRepo.Users[0].PasswordHash));
        Assert.NotNull(authRepo.ResetTokens[0].UsedAt);
    }

    [Fact]
    public async Task ARejectedResetMailsNothing()
    {
        var (authRepo, userRepo) = Fixture();
        var mail = new FakeEmailSender();
        var service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await Assert.ThrowsAnyAsync<Exception>(() => service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = "not-a-token",
            Password = NewPassword,
            PasswordConfirm = NewPassword
        }));

        Assert.Empty(mail.Sent);
    }

    // ---- helpers ---------------------------------------------------------

    private static (FakeAuthRepository, FakeUserRepository) Fixture()
    {
        var authRepo = new FakeAuthRepository();
        var userRepo = new FakeUserRepository();

        userRepo.Users.Add(new User
        {
            Email = Email,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword("Old-pass1!"),
            EmailConfirmed = true,
            EmailConfirmedAt = DateTime.UtcNow
        });

        return (authRepo, userRepo);
    }

    /// <summary>Runs the real forgot-password path, then spends the link it mailed.</summary>
    private static async Task Reset(
        FakeAuthRepository authRepo, FakeUserRepository userRepo, FakeEmailSender mail)
    {
        var service = AuthServiceFactory.Create(authRepo, userRepo, mail);

        await service.ForgotPasswordAsync(Email);
        var token = mail.Sent[0].Text.Split(StubResetLinkBuilder.Base)[1].Split(' ')[0];

        await service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = token,
            Password = NewPassword,
            PasswordConfirm = NewPassword
        });
    }
}
