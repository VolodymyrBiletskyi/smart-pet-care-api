using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Domain;
using Xunit;

namespace smart_pet_care_api.Modules.NotificationModule.Tests;

/// <summary>
/// Logout is where a push registration has to stop following the session. Lives in the
/// notification tests because that is the behaviour under test; AuthService's own flows
/// are covered by their fakes throwing.
/// </summary>
public class AuthServiceLogoutTests
{
    private const string Token = "fcm-token-of-the-phone";

    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task LogoutAsync_UnregistersTheDeviceThatIsSigningOut()
    {
        var authRepo = new FakeAuthRepository();
        authRepo.ActiveTokens.Add(new RefreshToken { UserId = _userId });
        var deviceTokens = new FakeDeviceTokenService();

        await Service(authRepo, deviceTokens).LogoutAsync(_userId, Token);

        Assert.Equal((_userId, Token), Assert.Single(deviceTokens.Unregistered));
    }

    /// <summary>
    /// A client that sends no token logs out exactly as before. The registration then
    /// survives, which is why reassignment on register is the other half of the fix.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LogoutAsync_WithoutADeviceToken_LeavesRegistrationsAlone(string? token)
    {
        var authRepo = new FakeAuthRepository();
        authRepo.ActiveTokens.Add(new RefreshToken { UserId = _userId });
        var deviceTokens = new FakeDeviceTokenService();

        await Service(authRepo, deviceTokens).LogoutAsync(_userId, token);

        Assert.Empty(deviceTokens.Unregistered);
        Assert.Equal(1, authRepo.SaveChangesCalls);
    }

    [Fact]
    public async Task LogoutAsync_StillRevokesEveryActiveRefreshToken()
    {
        var authRepo = new FakeAuthRepository();
        var mine = new RefreshToken { UserId = _userId };
        var someoneElses = new RefreshToken { UserId = Guid.NewGuid() };
        authRepo.ActiveTokens.Add(mine);
        authRepo.ActiveTokens.Add(someoneElses);

        await Service(authRepo, new FakeDeviceTokenService()).LogoutAsync(_userId, Token);

        Assert.NotNull(mine.RevokedAt);
        Assert.Null(someoneElses.RevokedAt);
        Assert.Equal(1, authRepo.SaveChangesCalls);
    }

    /// <summary>
    /// The refresh tokens may already be gone -- an expired session, or a second logout
    /// call -- and returning there used to be the whole method. The device still has to
    /// be unregistered, otherwise the one case where a client retries a failed logout is
    /// the case where the push registration is kept.
    /// </summary>
    [Fact]
    public async Task LogoutAsync_UnregistersEvenWithNoActiveRefreshTokens()
    {
        var authRepo = new FakeAuthRepository();
        var deviceTokens = new FakeDeviceTokenService();

        await Service(authRepo, deviceTokens).LogoutAsync(_userId, Token);

        Assert.Equal((_userId, Token), Assert.Single(deviceTokens.Unregistered));
        Assert.Equal(0, authRepo.SaveChangesCalls);
    }

    /// <summary>
    /// The unregistration races the push sender's own pruning of the same row, so it runs
    /// last: a failure there is a 500 the client can retry, while a session left unrevoked
    /// stays usable for the refresh token's full seven days.
    /// </summary>
    [Fact]
    public async Task LogoutAsync_RevokesTheSessionEvenWhenUnregisteringFails()
    {
        var authRepo = new FakeAuthRepository();
        var mine = new RefreshToken { UserId = _userId };
        authRepo.ActiveTokens.Add(mine);
        var deviceTokens = new FakeDeviceTokenService { ThrowOnUnregister = true };

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => Service(authRepo, deviceTokens).LogoutAsync(_userId, Token));

        Assert.NotNull(mine.RevokedAt);
        Assert.Equal(1, authRepo.SaveChangesCalls);
    }

    private static AuthService Service(
        FakeAuthRepository authRepo, FakeDeviceTokenService deviceTokens) =>
        new(new UnusedJwtProvider(),
            new UnusedUserRepository(),
            authRepo,
            new UnusedGoogleOAuth(),
            new UnusedEmailSender(),
            deviceTokens);
}
