using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using Testcontainers.PostgreSql;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Tests;

/// <summary>
/// The sweep is one <c>ExecuteUpdate</c> statement, so its predicate never runs in C# and the
/// fake repository cannot stand in for it: the in-memory provider does not implement
/// <c>ExecuteUpdate</c> at all. Requires Docker, like DeviceTokenRepositoryPostgresTests.
/// </summary>
public sealed class ReminderRunSweepPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:16").Build();

    private Guid petId;

    public async ValueTask InitializeAsync()
    {
        await database.StartAsync();
        await using var dbContext = CreateContext();
        await dbContext.Database.MigrateAsync();

        var user = new User { Email = $"run-sweep-{Guid.NewGuid():N}@example.com", PasswordHash = "hash" };
        var pet = new Pet { UserId = user.Id, Name = "Test", Species = AnimalSpecies.Dog };
        dbContext.AddRange(user, pet);
        await dbContext.SaveChangesAsync();

        petId = pet.Id;
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();

    [Theory]
    [InlineData(ReminderRunStatus.Pending)]
    [InlineData(ReminderRunStatus.Sent)]
    [InlineData(ReminderRunStatus.Delivered)]
    public async Task OpenRunsPastTheCutoffAreClosedAsMissed(ReminderRunStatus status)
    {
        var scheduledFor = DateTime.UtcNow.AddDays(-2);
        var runId = await SeedRunAsync(status, scheduledFor);

        await using var context = CreateContext();
        var closed = await new ReminderRepository(context)
            .MarkOverdueRunsMissedAsync(DateTime.UtcNow.AddHours(-24));

        Assert.Equal(1, closed);

        var run = await ReadRunAsync(runId);
        Assert.Equal(ReminderRunStatus.Missed, run.Status);
        Assert.NotNull(run.UpdatedAt);
        // The sweep is about the answer, not the delivery: SentAt is history and stays put,
        // which also keeps CK_ReminderRun_TimingOrder satisfied.
        Assert.Equal(scheduledFor, run.SentAt!.Value, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(ReminderRunStatus.Completed)]
    [InlineData(ReminderRunStatus.Cancelled)]
    [InlineData(ReminderRunStatus.Missed)]
    [InlineData(ReminderRunStatus.Failed)]
    public async Task SettledRunsAreLeftAlone(ReminderRunStatus status)
    {
        var runId = await SeedRunAsync(status, DateTime.UtcNow.AddDays(-9));

        await using var context = CreateContext();
        var closed = await new ReminderRepository(context)
            .MarkOverdueRunsMissedAsync(DateTime.UtcNow.AddHours(-24));

        Assert.Equal(0, closed);
        Assert.Equal(status, (await ReadRunAsync(runId)).Status);
    }

    /// <summary>
    /// A completion that arrived without the status being advanced — the belt to
    /// <c>CompletedAt == null</c>'s braces — must not be relabelled as a miss.
    /// </summary>
    [Fact]
    public async Task ARunCarryingACompletionIsLeftAlone()
    {
        var runId = await SeedRunAsync(
            ReminderRunStatus.Sent, DateTime.UtcNow.AddDays(-4), completedAt: DateTime.UtcNow.AddDays(-4));

        await using var context = CreateContext();
        var closed = await new ReminderRepository(context)
            .MarkOverdueRunsMissedAsync(DateTime.UtcNow.AddHours(-24));

        Assert.Equal(0, closed);
        Assert.Equal(ReminderRunStatus.Sent, (await ReadRunAsync(runId)).Status);
    }

    [Fact]
    public async Task AnOccurrenceInsideTheGraceIsLeftAlone()
    {
        var runId = await SeedRunAsync(ReminderRunStatus.Sent, DateTime.UtcNow.AddHours(-3));

        await using var context = CreateContext();
        var closed = await new ReminderRepository(context)
            .MarkOverdueRunsMissedAsync(DateTime.UtcNow.AddHours(-24));

        Assert.Equal(0, closed);
        var run = await ReadRunAsync(runId);
        Assert.Equal(ReminderRunStatus.Sent, run.Status);
        Assert.Null(run.UpdatedAt);
    }

    /// <summary>
    /// The sweep is not scoped to a reminder, which is the whole point: a Once rule is already
    /// Completed by the time its own occurrence goes unanswered.
    /// </summary>
    [Fact]
    public async Task RunsOfAnExhaustedRuleAreSweptToo()
    {
        var runId = await SeedRunAsync(
            ReminderRunStatus.Sent, DateTime.UtcNow.AddDays(-5),
            repeatType: RepeatType.Once, reminderStatus: ReminderStatus.Completed);

        await using var context = CreateContext();
        var closed = await new ReminderRepository(context)
            .MarkOverdueRunsMissedAsync(DateTime.UtcNow.AddHours(-24));

        Assert.Equal(1, closed);
        Assert.Equal(ReminderRunStatus.Missed, (await ReadRunAsync(runId)).Status);
    }

    private async Task<Guid> SeedRunAsync(
        ReminderRunStatus status, DateTime scheduledFor, DateTime? completedAt = null,
        RepeatType repeatType = RepeatType.Daily, ReminderStatus reminderStatus = ReminderStatus.Active)
    {
        await using var context = CreateContext();

        var reminder = new Reminder
        {
            PetId = petId,
            Title = "Morning meal",
            Type = ReminderType.Feeding,
            RepeatType = repeatType,
            Status = reminderStatus,
            TimeOfDay = new TimeSpan(8, 0, 0),
            UtcOffsetMinutes = 60,
            StartAt = scheduledFor,
            ScheduleAnchorAt = scheduledFor,
            NextTriggerAt = reminderStatus == ReminderStatus.Active ? scheduledFor.AddDays(1) : null
        };

        var run = new ReminderRun
        {
            ReminderId = reminder.Id,
            ScheduledFor = scheduledFor,
            SentAt = scheduledFor,
            CompletedAt = completedAt,
            Status = status,
            Type = ReminderType.Feeding,
            Channel = "push"
        };

        context.AddRange(reminder, run);
        await context.SaveChangesAsync();

        return run.Id;
    }

    private async Task<ReminderRun> ReadRunAsync(Guid runId)
    {
        await using var context = CreateContext();
        return await context.ReminderRuns.SingleAsync(
            rr => rr.Id == runId, TestContext.Current.CancellationToken);
    }

    private AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options);
    }
}
