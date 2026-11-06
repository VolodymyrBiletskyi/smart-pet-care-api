using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Domain;
using smart_pet_care_api.Modules.AuthModule.DTOs.Requests;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

/// <summary>
/// What a session is worth once it is over: rotation and reuse detection on the
/// refresh token, and the access token in hand, which neither a logout nor a reset
/// used to reach.
/// </summary>
public class SessionLifetimeTests
{
    private const string Email = "owner@example.com";
    private const string Password = "Owner-pass1!";

    // ---- rotation and reuse ----------------------------------------------

    [Fact]
    public async Task Refresh_RotatesWithinOneFamilyAndRecordsTheReplacement()
    {
        var (authRepo, userRepo, service) = Fixture();
        var signIn = await service.LoginAsync(Login());

        var refreshed = await service.RefreshAsync(signIn.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.Equal(2, authRepo.RefreshTokens.Count);

        var spent = authRepo.RefreshTokens[0];
        var issued = authRepo.RefreshTokens[1];

        Assert.NotNull(spent.RevokedAt);
        Assert.Equal(issued.TokenHash, spent.ReplacedByTokenHash);
        Assert.Equal(spent.TokenFamilyId, issued.TokenFamilyId);
        Assert.True(issued.IsActive);
    }

    [Fact]
    public async Task Login_StartsAFamilyOfItsOwn()
    {
        var (authRepo, userRepo, service) = Fixture();

        await service.LoginAsync(Login());
        await service.LoginAsync(Login());

        Assert.Equal(2, authRepo.RefreshTokens.Select(t => t.TokenFamilyId).Distinct().Count());
    }

    /// <summary>
    /// A token that was already spent coming back is either a replay or a client that
    /// lost the answer. Rotation cannot tell which, so the session ends — and the
    /// request is refused either way, which is why the grace below costs nothing.
    /// </summary>
    [Fact]
    public async Task Refresh_WithASpentTokenEndsTheWholeFamily()
    {
        var (authRepo, userRepo, service) = Fixture();
        var signIn = await service.LoginAsync(Login());
        var refreshed = await service.RefreshAsync(signIn.RefreshToken);
        Age(authRepo);

        var replayed = await service.RefreshAsync(signIn.RefreshToken);

        Assert.Null(replayed);
        Assert.All(authRepo.RefreshTokens, token => Assert.NotNull(token.RevokedAt));

        // And the token the honest client is holding is dead too, so it cannot carry on.
        Assert.Null(await service.RefreshAsync(refreshed!.RefreshToken));
    }

    [Fact]
    public async Task Refresh_WithASpentTokenLeavesOtherDevicesAlone()
    {
        var (authRepo, userRepo, service) = Fixture();
        var phone = await service.LoginAsync(Login());
        var tablet = await service.LoginAsync(Login());

        await service.RefreshAsync(phone.RefreshToken);
        Age(authRepo);
        await service.RefreshAsync(phone.RefreshToken);

        // The tablet never presented anything twice, so its session is untouched.
        Assert.NotNull(await service.RefreshAsync(tablet.RefreshToken));
    }

    /// <summary>
    /// A client with two requests in flight refreshes twice and the loser presents a
    /// token that was replaced a moment ago. It is refused, like any spent token, but
    /// the user is not signed out of the device for it.
    /// </summary>
    [Fact]
    public async Task Refresh_ARetryInsideTheGraceDoesNotEndTheSession()
    {
        var (authRepo, userRepo, service) = Fixture();
        var signIn = await service.LoginAsync(Login());
        var refreshed = await service.RefreshAsync(signIn.RefreshToken);

        var retried = await service.RefreshAsync(signIn.RefreshToken);

        Assert.Null(retried);
        Assert.NotNull(await service.RefreshAsync(refreshed!.RefreshToken));
    }

    /// <summary>
    /// Revoked by a logout rather than replaced by a refresh: there is no newer token
    /// for a client to be holding, so there is no benign reading of it coming back.
    /// </summary>
    [Fact]
    public async Task Refresh_WithALoggedOutTokenIsRefusedImmediately()
    {
        var (authRepo, userRepo, service) = Fixture();
        var signIn = await service.LoginAsync(Login());
        await service.LogoutAsync(userRepo.Users[0].Id);

        Assert.Null(await service.RefreshAsync(signIn.RefreshToken));
    }

    [Fact]
    public async Task Refresh_WithAnUnknownTokenChangesNothing()
    {
        var (authRepo, userRepo, service) = Fixture();
        await service.LoginAsync(Login());

        Assert.Null(await service.RefreshAsync("not-a-token"));
        Assert.All(authRepo.RefreshTokens, token => Assert.Null(token.RevokedAt));
    }

    // ---- logout reaches every session and the access token ---------------

    /// <summary>
    /// Logout used to revoke the ten newest live tokens, so an eleventh went on
    /// working for the rest of its seven days.
    /// </summary>
    [Fact]
    public async Task Logout_RevokesEveryLiveRefreshTokenNotJustTheNewestTen()
    {
        var (authRepo, userRepo, service) = Fixture();
        var user = userRepo.Users[0];

        for (var session = 0; session < 12; session++)
            await service.LoginAsync(Login());

        await service.LogoutAsync(user.Id);

        Assert.Equal(12, authRepo.RefreshTokens.Count);
        Assert.All(authRepo.RefreshTokens, token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task Logout_MovesTheAccessTokenCutoffAndDropsTheCachedState()
    {
        var (authRepo, userRepo, service) = Fixture();
        var state = new FakeAuthStateCache();
        service = AuthServiceFactory.Create(authRepo, userRepo, authState: state);
        var user = userRepo.Users[0];

        var before = DateTime.UtcNow;
        await service.LogoutAsync(user.Id);

        Assert.NotNull(user.TokensValidFrom);
        Assert.True(user.TokensValidFrom >= before);
        Assert.Contains(user.Id, state.Invalidated);
    }

    [Fact]
    public async Task Logout_UnregistersOnlyTheSigningOutDevice()
    {
        var (authRepo, userRepo, service) = Fixture();
        var devices = new FakeDeviceTokenService();
        service = AuthServiceFactory.Create(authRepo, userRepo, deviceTokens: devices);

        await service.LogoutAsync(userRepo.Users[0].Id, "device-a");

        Assert.Equal((userRepo.Users[0].Id, "device-a"), Assert.Single(devices.Unregistered));
        Assert.Empty(devices.UnregisteredAll);
    }

    // ---- helpers ---------------------------------------------------------

    private static (FakeAuthRepository, FakeUserRepository, AuthService) Fixture()
    {
        var authRepo = new FakeAuthRepository();
        var userRepo = new FakeUserRepository();

        userRepo.Users.Add(new User
        {
            Email = Email,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(Password),
            EmailConfirmed = true,
            EmailConfirmedAt = DateTime.UtcNow
        });

        return (authRepo, userRepo, AuthServiceFactory.Create(authRepo, userRepo));
    }

    private static LoginRequest Login() => new() { Email = Email, Password = Password };

    /// <summary>
    /// Pushes every revocation past the ten-second retry grace, so a token coming back
    /// is read as a replay rather than as the client asking twice.
    /// </summary>
    private static void Age(FakeAuthRepository authRepo)
    {
        foreach (var token in authRepo.RefreshTokens.Where(t => t.RevokedAt is not null))
            token.RevokedAt = token.RevokedAt!.Value.AddMinutes(-5);
    }
}
