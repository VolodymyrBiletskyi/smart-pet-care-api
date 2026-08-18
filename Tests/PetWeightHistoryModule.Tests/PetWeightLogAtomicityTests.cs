using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Domain;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Repository;
using Xunit;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Tests;

public sealed class PetWeightLogAtomicityTests
{
    [Fact]
    public async Task CreateAsync_PersistsLogAndCurrentWeightTogether()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var (petId, userId) = await SeedPetAsync(dbContext);
        var measuredAt = DateTime.UtcNow.AddMinutes(-5);
        var service = new PetWeightLogService(
            new PetWeightLogRepository(dbContext),
            new FakeReminderRecalculationService());

        await service.CreateAsync(
            petId,
            userId,
            Measurement(12.3m, measuredAt));

        dbContext.ChangeTracker.Clear();
        var pet = await dbContext.Pets.SingleAsync(
            candidate => candidate.Id == petId,
            TestContext.Current.CancellationToken);
        var log = await dbContext.PetWeightLogs.SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(12.3m, pet.WeightKg);
        Assert.Equal(12.3m, log.WeightKg);
        Assert.Equal(measuredAt, log.MeasuredAt);
    }

    [Fact]
    public async Task CreateAsync_WhenSaveFails_PersistsNeitherLogNorCurrentWeight()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        Guid petId;
        Guid userId;
        await using (var dbContext = new AppDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            (petId, userId) = await SeedPetAsync(
                dbContext,
                5m,
                DateTime.UtcNow.AddDays(-1));
            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER FailWeightLogInsert BEFORE INSERT ON PetWeightLogs "
                + "BEGIN SELECT RAISE(ABORT, 'forced failure'); END;",
                TestContext.Current.CancellationToken);
            var service = new PetWeightLogService(
                new PetWeightLogRepository(dbContext),
                new FakeReminderRecalculationService());

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                service.CreateAsync(
                    petId,
                    userId,
                    Measurement(9m, DateTime.UtcNow.AddMinutes(-1))));
        }

        await using var verificationContext = new AppDbContext(options);
        var pet = await verificationContext.Pets.SingleAsync(
            candidate => candidate.Id == petId,
            TestContext.Current.CancellationToken);
        var logs = await verificationContext.PetWeightLogs
            .Where(log => log.PetId == petId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5m, pet.WeightKg);
        Assert.Single(logs);
        Assert.Equal(5m, logs[0].WeightKg);
    }

    [Fact]
    public async Task CreateAsync_RepeatedMeasurementsAppendHistoryAndCurrentWeightMatchesLatest()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var (petId, userId) = await SeedPetAsync(dbContext);
        var firstMeasuredAt = DateTime.UtcNow.AddHours(-1);
        var secondMeasuredAt = DateTime.UtcNow.AddMinutes(-5);
        var service = new PetWeightLogService(
            new PetWeightLogRepository(dbContext),
            new FakeReminderRecalculationService());

        await service.CreateAsync(petId, userId, Measurement(8m, firstMeasuredAt));
        await service.CreateAsync(petId, userId, Measurement(9.5m, secondMeasuredAt));

        dbContext.ChangeTracker.Clear();
        var logs = await dbContext.PetWeightLogs
            .Where(log => log.PetId == petId)
            .OrderBy(log => log.MeasuredAt)
            .ToListAsync(TestContext.Current.CancellationToken);
        var pet = await dbContext.Pets.SingleAsync(
            candidate => candidate.Id == petId,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, logs.Count);
        Assert.Equal(8m, logs[0].WeightKg);
        Assert.Equal(9.5m, logs[1].WeightKg);
        Assert.Equal(logs[^1].WeightKg, pet.WeightKg);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static DbContextOptions<AppDbContext> CreateOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

    private static async Task<(Guid PetId, Guid UserId)> SeedPetAsync(
        AppDbContext dbContext,
        decimal? weight = null,
        DateTime? measuredAt = null)
    {
        var user = new User { Email = $"weight-{Guid.NewGuid():N}@example.com", PasswordHash = "hash" };
        var pet = new Pet
        {
            UserId = user.Id,
            Name = "Buddy",
            Species = Enums.AnimalSpecies.Dog,
            WeightKg = weight
        };
        dbContext.AddRange(user, pet);
        if (weight.HasValue && measuredAt.HasValue)
        {
            dbContext.PetWeightLogs.Add(new PetWeightLog
            {
                PetId = pet.Id,
                WeightKg = weight.Value,
                MeasuredAt = measuredAt.Value,
                CreatedAt = measuredAt.Value
            });
        }

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (pet.Id, user.Id);
    }

    private static CreatePetWeightLogDto Measurement(decimal weight, DateTime measuredAt) => new()
    {
        WeightKg = weight,
        MeasuredAt = measuredAt,
        Notes = "Measured by user"
    };
}
