using Microsoft.EntityFrameworkCore;
using Npgsql;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NotificationModule.Repository;
using Testcontainers.PostgreSql;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NotificationModule.Tests;

/// <summary>
/// The upsert is raw SQL resolved by a unique index, so neither half can be checked
/// against the in-memory provider: the ON CONFLICT target only exists in the migration.
/// Requires Docker, like ChatSessionConcurrencyPostgresTests.
/// </summary>
public sealed class DeviceTokenRepositoryPostgresTests : IAsyncLifetime
{
    private const string Token = "fcm-token-of-one-handset";

    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:16").Build();

    public async ValueTask InitializeAsync()
    {
        await database.StartAsync();
        await using var dbContext = CreateContext();
        await dbContext.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();

    /// <summary>
    /// The case behind the fix: a handset signed out of one account and into another. The
    /// registration has to move, because a leftover row for the first account keeps that
    /// account's reminders -- pet name and description included -- arriving on a phone
    /// somebody else is now using.
    /// </summary>
    [Fact]
    public async Task UpsertAsync_MovesTheTokenToTheAccountThatRegisteredItLast()
    {
        var (first, second) = await SeedTwoUsersAsync();

        await Repository(out var firstContext).UpsertAsync(new DeviceToken
        {
            UserId = first,
            Token = Token,
            Platform = DevicePlatform.Android
        });
        await firstContext.DisposeAsync();

        await Repository(out var secondContext).UpsertAsync(new DeviceToken
        {
            UserId = second,
            Token = Token,
            Platform = DevicePlatform.iOS
        });
        await secondContext.DisposeAsync();

        await using var assertContext = CreateContext();
        var repo = new DeviceTokenRepository(assertContext);

        var row = Assert.Single(await repo.GetByUserIdAsync(second));
        Assert.Equal(Token, row.Token);
        Assert.Equal(DevicePlatform.iOS, row.Platform);

        Assert.Empty(await repo.GetByUserIdAsync(first));
        Assert.Equal(1, await assertContext.DeviceTokens.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpsertAsync_IsIdempotentForTheSameUserAndBumpsLastSeen()
    {
        var (user, _) = await SeedTwoUsersAsync();
        var firstSeenAt = DateTime.UtcNow.AddDays(-3);

        await Repository(out var context).UpsertAsync(new DeviceToken
        {
            UserId = user,
            Token = Token,
            Platform = DevicePlatform.Android,
            CreatedAt = firstSeenAt,
            LastSeenAt = firstSeenAt
        });
        await context.DisposeAsync();

        var secondSeenAt = DateTime.UtcNow;
        await Repository(out var again).UpsertAsync(new DeviceToken
        {
            UserId = user,
            Token = Token,
            Platform = DevicePlatform.Android,
            CreatedAt = secondSeenAt,
            LastSeenAt = secondSeenAt
        });
        await again.DisposeAsync();

        await using var assertContext = CreateContext();
        var row = Assert.Single(
            await assertContext.DeviceTokens.ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(secondSeenAt, row.LastSeenAt, TimeSpan.FromSeconds(1));
        // CreatedAt is not in the DO UPDATE list: the device registered once and has been
        // seen again, so overwriting it would lose when it first appeared.
        Assert.Equal(firstSeenAt, row.CreatedAt, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The guarantee the upsert leans on. Without the index ON CONFLICT ("Token") is a
    /// Postgres error rather than an upsert, so a lost migration would fail every
    /// registration instead of quietly going back to one row per account.
    /// </summary>
    [Fact]
    public async Task TokenIsUniqueAcrossAccountsAtTheDatabaseLevel()
    {
        var (first, second) = await SeedTwoUsersAsync();

        await using var dbContext = CreateContext();
        dbContext.DeviceTokens.AddRange(
            new DeviceToken { UserId = first, Token = Token },
            new DeviceToken { UserId = second, Token = Token });

        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        var postgresError = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresError.SqlState);
    }

    private DeviceTokenRepository Repository(out AppDbContext context)
    {
        context = CreateContext();
        return new DeviceTokenRepository(context);
    }

    private AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options);
    }

    private async Task<(Guid First, Guid Second)> SeedTwoUsersAsync()
    {
        await using var dbContext = CreateContext();
        var first = new User { Email = $"device-token-{Guid.NewGuid():N}@example.com", PasswordHash = "hash" };
        var second = new User { Email = $"device-token-{Guid.NewGuid():N}@example.com", PasswordHash = "hash" };
        dbContext.AddRange(first, second);
        await dbContext.SaveChangesAsync();
        return (first.Id, second.Id);
    }
}
