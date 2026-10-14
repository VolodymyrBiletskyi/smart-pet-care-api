using smart_pet_care_api.Common.Api;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NotificationModule.Domain;
using smart_pet_care_api.Modules.NotificationModule.DTOs.Requests;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NotificationModule.Tests;

public class DeviceTokenServiceTests
{
    private const string Token = "fcm-token-aaa";

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_RejectsBlankToken(string? token)
    {
        var repo = new FakeDeviceTokenRepository();

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repo).RegisterAsync(_userId, new RegisterDeviceTokenDto { Token = token! }));

        Assert.Equal(ErrorCodes.Notification.DeviceTokenRequired, error.Code);
        Assert.Empty(repo.Upserted);
    }

    [Fact]
    public async Task RegisterAsync_TrimsTokenAndRecordsPlatform()
    {
        var repo = new FakeDeviceTokenRepository();

        await Service(repo).RegisterAsync(_userId, new RegisterDeviceTokenDto
        {
            Token = $"  {Token}  ",
            Platform = DevicePlatform.iOS
        });

        var upserted = Assert.Single(repo.Upserted);
        Assert.Equal(Token, upserted.Token);
        Assert.Equal(_userId, upserted.UserId);
        Assert.Equal(DevicePlatform.iOS, upserted.Platform);
        Assert.Equal(upserted.CreatedAt, upserted.LastSeenAt);
    }

    /// <summary>
    /// Registration is the only thing that prunes -- there is no sweeper job -- so the
    /// 60-day window is pinned here. Widen it and tokens FCM has long since rotated keep
    /// being sent to; narrow it and a user who opens the app rarely stops getting pushes.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_PrunesStaleRegistrationsBeforeInserting()
    {
        var repo = new FakeDeviceTokenRepository();
        var before = DateTime.UtcNow;

        await Service(repo).RegisterAsync(_userId, new RegisterDeviceTokenDto { Token = Token });

        Assert.Equal(
            new[] { "RemoveStaleAsync", "UpsertAsync" },
            repo.Calls);

        Assert.NotNull(repo.StaleCutoff);
        var age = before - repo.StaleCutoff!.Value;
        Assert.InRange(age.TotalDays, 59.9, 60.1);
    }

    /// <summary>
    /// The registration follows whoever is signed in on the device. Two rows for one token
    /// is what used to leave the previous account's reminders arriving on a phone that had
    /// been handed over or signed out of.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_MovesATokenAlreadyHeldByAnotherAccount()
    {
        var repo = new FakeDeviceTokenRepository();
        repo.Rows.Add(new DeviceToken
        {
            UserId = _otherUserId,
            Token = Token,
            LastSeenAt = DateTime.UtcNow
        });

        await Service(repo).RegisterAsync(_userId, new RegisterDeviceTokenDto { Token = Token });

        var row = Assert.Single(repo.Rows);
        Assert.Equal(_userId, row.UserId);

        Assert.Empty(await repo.GetByUserIdAsync(_otherUserId));
    }

    [Fact]
    public async Task UnregisterAsync_RemovesOnlyTheCallersOwnRow()
    {
        var repo = new FakeDeviceTokenRepository();
        repo.Rows.Add(new DeviceToken { UserId = _otherUserId, Token = Token });

        var removed = await Service(repo).UnregisterAsync(_userId, Token);

        Assert.False(removed);
        Assert.Single(repo.Rows);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UnregisterAsync_TrimsAndSavesWhenTheRowIsTheCallers()
    {
        var repo = new FakeDeviceTokenRepository();
        repo.Rows.Add(new DeviceToken { UserId = _userId, Token = Token });

        var removed = await Service(repo).UnregisterAsync(_userId, $" {Token} ");

        Assert.True(removed);
        Assert.Empty(repo.Rows);
        Assert.Equal(1, repo.SaveChangesCalls);
        Assert.Equal((_userId, Token), Assert.Single(repo.ScopedRemovals));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnregisterAsync_IgnoresBlankTokenWithoutTouchingTheTable(string token)
    {
        var repo = new FakeDeviceTokenRepository();

        Assert.False(await Service(repo).UnregisterAsync(_userId, token));
        Assert.Empty(repo.Calls);
    }

    private static DeviceTokenService Service(FakeDeviceTokenRepository repo) => new(repo);
}
