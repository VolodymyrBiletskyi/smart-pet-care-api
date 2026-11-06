using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Infrastructure;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using smart_pet_care_api.Modules.UserModule.Repository;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

/// <summary>
/// The middleware is where a signed, unexpired access token stops being enough. Up
/// to here it only asked whether the account still existed, which is why signing out
/// left the token in hand working for the rest of its fifteen minutes.
/// </summary>
public class AuthMiddlewareTests
{
    [Fact]
    public async Task AnAnonymousRequestIsNotLookedUpAtAll()
    {
        var userRepo = new FakeUserRepository();
        var context = Context(principal: null);

        var reached = await Invoke(userRepo, context);

        Assert.True(reached);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ATokenForALiveAccountWithNoCutoffGoesThrough()
    {
        var (userRepo, user) = Fixture();
        var context = Context(Principal(user.Id, DateTime.UtcNow));

        Assert.True(await Invoke(userRepo, context));
    }

    [Fact]
    public async Task ATokenForADeletedAccountIsRefused()
    {
        var userRepo = new FakeUserRepository();
        var context = Context(Principal(Guid.NewGuid(), DateTime.UtcNow));

        Assert.False(await Invoke(userRepo, context));
        await AssertRefusedWith(context, ErrorCodes.Auth.AccountNoLongerExists);
    }

    [Fact]
    public async Task ATokenIssuedBeforeTheCutoffIsRefused()
    {
        var (userRepo, user) = Fixture();
        user.TokensValidFrom = DateTime.UtcNow;

        var context = Context(Principal(user.Id, DateTime.UtcNow.AddMinutes(-5)));

        Assert.False(await Invoke(userRepo, context));
        await AssertRefusedWith(context, ErrorCodes.Auth.SessionRevoked);
    }

    /// <summary>
    /// Signing in again after a sign-out has to work, which is the whole point of the
    /// cutoff being an instant rather than a flag.
    /// </summary>
    [Fact]
    public async Task ATokenIssuedAfterTheCutoffGoesThrough()
    {
        var (userRepo, user) = Fixture();
        user.TokensValidFrom = DateTime.UtcNow.AddMinutes(-5);

        var context = Context(Principal(user.Id, DateTime.UtcNow));

        Assert.True(await Invoke(userRepo, context));
    }

    /// <summary>
    /// Signing in again in the same second as the sign-out. The issue time is in
    /// milliseconds for this: at second granularity one of these two cases has to break,
    /// and both break for a quarter of an hour — either the new session is refused or the
    /// old token survives the sign-out.
    /// </summary>
    [Fact]
    public async Task TheCutoffSeparatesTokensWithinTheSameSecond()
    {
        var (userRepo, user) = Fixture();
        var cutoff = DateTime.UtcNow;
        user.TokensValidFrom = cutoff;

        var before = Context(Principal(user.Id, cutoff.AddMilliseconds(-2)));
        var after = Context(Principal(user.Id, cutoff.AddMilliseconds(2)));

        Assert.False(await Invoke(userRepo, before));
        Assert.True(await Invoke(userRepo, after));
    }

    /// <summary>
    /// Fails closed: the only tokens without the claim are those signed by a build that
    /// predates it, and they expire within fifteen minutes.
    /// </summary>
    [Fact]
    public async Task ATokenWithNoIssueTimeIsRefusedOnceACutoffExists()
    {
        var (userRepo, user) = Fixture();
        user.TokensValidFrom = DateTime.UtcNow;

        var context = Context(Principal(user.Id, issuedAt: null));

        Assert.False(await Invoke(userRepo, context));
        await AssertRefusedWith(context, ErrorCodes.Auth.SessionRevoked);
    }

    [Fact]
    public async Task ATokenWithNoIssueTimeGoesThroughWhenNothingWasRevoked()
    {
        var (userRepo, user) = Fixture();

        Assert.True(await Invoke(userRepo, Context(Principal(user.Id, issuedAt: null))));
    }

    // ---- the cache in front of it ----------------------------------------

    [Fact]
    public async Task TheCacheAnswersTheSecondRequestWithoutALookup()
    {
        var cache = new AuthStateCache(new MemoryCache(new MemoryCacheOptions()));
        var userId = Guid.NewGuid();
        var lookups = 0;

        Task<UserAuthState?> Load()
        {
            lookups++;
            return Task.FromResult<UserAuthState?>(new UserAuthState(null));
        }

        await cache.GetAsync(userId, Load);
        await cache.GetAsync(userId, Load);

        Assert.Equal(1, lookups);
    }

    /// <summary>
    /// Without this, "signed out at once" would mean "within three minutes".
    /// </summary>
    [Fact]
    public async Task InvalidatingTheCacheForcesAFreshLookup()
    {
        var cache = new AuthStateCache(new MemoryCache(new MemoryCacheOptions()));
        var userId = Guid.NewGuid();
        var cutoff = DateTime.UtcNow;

        await cache.GetAsync(userId, () => Task.FromResult<UserAuthState?>(new UserAuthState(null)));
        cache.Invalidate(userId);

        var state = await cache.GetAsync(
            userId, () => Task.FromResult<UserAuthState?>(new UserAuthState(cutoff)));

        Assert.Equal(cutoff, state!.TokensValidFrom);
    }

    /// <summary>A deleted account is cached as the absent state, not looked up every time.</summary>
    [Fact]
    public async Task TheCacheHoldsAMissingAccountToo()
    {
        var cache = new AuthStateCache(new MemoryCache(new MemoryCacheOptions()));
        var userId = Guid.NewGuid();
        var lookups = 0;

        Task<UserAuthState?> Load()
        {
            lookups++;
            return Task.FromResult<UserAuthState?>(null);
        }

        Assert.Null(await cache.GetAsync(userId, Load));
        Assert.Null(await cache.GetAsync(userId, Load));
        Assert.Equal(1, lookups);
    }

    // ---- helpers ---------------------------------------------------------

    private static (FakeUserRepository, User) Fixture()
    {
        var userRepo = new FakeUserRepository();
        var user = new User { Email = "owner@example.com" };
        userRepo.Users.Add(user);

        return (userRepo, user);
    }

    /// <summary>Runs the middleware and answers whether the request reached what follows it.</summary>
    private static async Task<bool> Invoke(FakeUserRepository userRepo, DefaultHttpContext context)
    {
        var reached = false;

        await new AuthMiddleware(userRepo, new FakeAuthStateCache())
            .InvokeAsync(context, _ =>
            {
                reached = true;
                return Task.CompletedTask;
            });

        return reached;
    }

    private static ClaimsPrincipal Principal(Guid userId, DateTime? issuedAt)
    {
        var claims = new List<Claim> { new(AuthClaims.UserId, userId.ToString()) };

        if (issuedAt is { } instant)
            claims.Add(new Claim(
                AuthClaims.TokenIssuedAt,
                new DateTimeOffset(instant, TimeSpan.Zero).ToUnixTimeMilliseconds().ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static DefaultHttpContext Context(ClaimsPrincipal? principal)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        if (principal is not null)
            context.User = principal;

        return context;
    }

    private static async Task AssertRefusedWith(DefaultHttpContext context, string code)
    {
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);

        Assert.Equal(code, body.GetProperty("code").GetString());
    }
}
