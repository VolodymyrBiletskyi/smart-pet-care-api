using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using Testcontainers.PostgreSql;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Tests;

/// <summary>
/// One container and one migration run for the whole class. Per-test containers cost 37
/// migrations each, and the sweep needs no isolation beyond an empty run table, which
/// <see cref="ReminderRunSweepPostgresTests.SeedRunsAsync"/> provides.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:16").Build();

    public Guid PetId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await database.StartAsync();
        await using var dbContext = CreateContext();
        await dbContext.Database.MigrateAsync();

        var user = new User { Email = $"run-sweep-{Guid.NewGuid():N}@example.com", PasswordHash = "hash" };
        var pet = new Pet { UserId = user.Id, Name = "Test", Species = AnimalSpecies.Dog };
        dbContext.AddRange(user, pet);
        await dbContext.SaveChangesAsync();

        PetId = pet.Id;
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();

    public AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options);
    }
}

/// <summary>
/// The sweep is one <c>ExecuteUpdate</c> statement, so its predicate never runs in C# and the
/// fake repository cannot stand in for it: the in-memory provider does not implement
/// <c>ExecuteUpdate</c> at all. Requires Docker, like DeviceTokenRepositoryPostgresTests.
/// </summary>
public sealed class ReminderRunSweepPostgresTests : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan Grace = TimeSpan.FromHours(24);

    private readonly PostgresFixture fixture;

    public ReminderRunSweepPostgresTests(PostgresFixture fixture) => this.fixture = fixture;

    [Theory]
    [InlineData(ReminderRunStatus.Pending)]
    [InlineData(ReminderRunStatus.Sent)]
    [InlineData(ReminderRunStatus.Delivered)]
    public async Task OpenRunsPastTheCutoffAreClosedAsMissed(ReminderRunStatus status)
    {
        var scheduledFor = DateTime.UtcNow.AddDays(-2);
        var runId = await SeedRunAsync(status, scheduledFor);

        Assert.Equal(1, await SweepAsync());

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

        Assert.Equal(0, await SweepAsync());
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

        Assert.Equal(0, await SweepAsync());
        Assert.Equal(ReminderRunStatus.Sent, (await ReadRunAsync(runId)).Status);
    }

    [Fact]
    public async Task AnOccurrenceInsideTheGraceIsLeftAlone()
    {
        var runId = await SeedRunAsync(ReminderRunStatus.Sent, DateTime.UtcNow.AddHours(-3));

        Assert.Equal(0, await SweepAsync());

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

        Assert.Equal(1, await SweepAsync());
        Assert.Equal(ReminderRunStatus.Missed, (await ReadRunAsync(runId)).Status);
    }

    /// <summary>
    /// The grace runs from when the user could first see the notification, so both the slot and
    /// the row have to be old. A scheduler back from an outage longer than the grace fires a
    /// trigger days in the past; on the slot alone the push it has just sent would be called
    /// missed on the next tick, thirty seconds later.
    /// </summary>
    [Fact]
    public async Task ARunJustCreatedForALongOverdueSlotIsSpared()
    {
        var missedSlot = DateTime.UtcNow.AddDays(-3);

        var justFired = await SeedRunAsync(
            ReminderRunStatus.Sent, missedSlot, createdAt: DateTime.UtcNow, clear: true);
        var genuinelyStale = await SeedRunAsync(
            ReminderRunStatus.Sent, missedSlot, createdAt: missedSlot, clear: false);

        Assert.Equal(1, await SweepAsync());

        Assert.Equal(ReminderRunStatus.Sent, (await ReadRunAsync(justFired)).Status);
        Assert.Equal(ReminderRunStatus.Missed, (await ReadRunAsync(genuinelyStale)).Status);
    }

    private async Task<int> SweepAsync()
    {
        await using var context = fixture.CreateContext();
        return await new ReminderRepository(context).MarkOverdueRunsMissedAsync(DateTime.UtcNow - Grace);
    }

    /// <summary>
    /// Clears the run table first, since the sweep spans every reminder and the returned count
    /// is what the assertions turn on. Tests in one class do not run in parallel.
    /// </summary>
    private async Task<Guid> SeedRunAsync(
        ReminderRunStatus status, DateTime scheduledFor, DateTime? completedAt = null,
        RepeatType repeatType = RepeatType.Daily, ReminderStatus reminderStatus = ReminderStatus.Active,
        DateTime? createdAt = null, bool clear = true)
    {
        await using var context = fixture.CreateContext();

        if (clear)
        {
            await context.ReminderRuns.ExecuteDeleteAsync();
            await context.Reminders.ExecuteDeleteAsync();
        }

        var reminder = new Reminder
        {
            PetId = fixture.PetId,
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
            // The slot is when the run would ordinarily have been created; a test about an old
            // slot on a fresh row passes its own value.
            CreatedAt = createdAt ?? scheduledFor,
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
        await using var context = fixture.CreateContext();
        return await context.ReminderRuns.SingleAsync(
            rr => rr.Id == runId, TestContext.Current.CancellationToken);
    }
}
