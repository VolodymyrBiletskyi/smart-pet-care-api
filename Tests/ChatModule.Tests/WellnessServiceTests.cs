using System.Net;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Modules.WellnessModule.Domain;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class WellnessServiceTests
{
    [Fact]
    public async Task RecalculateAsync_PersistsValidClassifierResponse()
    {
        await using var db = CreateContext();
        var petId = Guid.NewGuid();
        var service = CreateService(db, new RecordingClassifier());

        var result = await service.RecalculateAsync(
            petId, Guid.NewGuid(), null, TestContext.Current.CancellationToken);

        Assert.Equal(82, result.Result.WellnessScore);
        var stored = Assert.Single(await db.PetWellnessAssessments.ToListAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("1.0.0", stored.CalculationVersion);
        Assert.Contains("wellnessScore", stored.ResponseJson);
    }

    [Fact]
    public async Task RecalculateAsync_DoesNotPersistFailedCalculation()
    {
        await using var db = CreateContext();
        var service = CreateService(db, new RecordingClassifier(shouldFail: true));

        await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            service.RecalculateAsync(
                Guid.NewGuid(), Guid.NewGuid(), null, TestContext.Current.CancellationToken));

        Assert.Empty(await db.PetWellnessAssessments.ToListAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecalculateAsync_SerializesConcurrentCallsForSamePet()
    {
        await using var db = CreateContext();
        var classifier = new RecordingClassifier(delay: TimeSpan.FromMilliseconds(75));
        var service = CreateService(db, classifier);
        var petId = Guid.NewGuid();

        await Task.WhenAll(
            service.RecalculateAsync(petId, Guid.NewGuid(), null, TestContext.Current.CancellationToken),
            service.RecalculateAsync(petId, Guid.NewGuid(), null, TestContext.Current.CancellationToken));

        Assert.Equal(1, classifier.MaximumConcurrentCalls);
        Assert.Equal(2, await db.PetWellnessAssessments.CountAsync(TestContext.Current.CancellationToken));
    }

    private static WellnessService CreateService(AppDbContext db, IClassifierClient classifier) =>
        new(
            db,
            new StubAggregator(),
            classifier,
            new WellnessCalculationLock(),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero)));

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class StubAggregator : IWellnessDataAggregator
    {
        public Task<ClassifierWellnessRequest> AggregateAsync(
            Guid petId,
            Guid userId,
            string? currentSymptoms,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClassifierWellnessRequest
            {
                Pet = new ClassifierWellnessPet { Species = "dog" },
                CurrentSymptoms = currentSymptoms
            });
    }

    private sealed class RecordingClassifier(bool shouldFail = false, TimeSpan? delay = null)
        : IClassifierClient
    {
        private int activeCalls;
        public int MaximumConcurrentCalls { get; private set; }

        public Task<ClassifierChatResponse> ChatAsync(
            ClassifierChatRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ClassifierFeedingSummaryResponse> SummarizeFeedingAsync(
            ClassifierFeedingSummaryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<ClassifierWellnessResponse> CalculateWellnessAsync(
            ClassifierWellnessRequest request, CancellationToken cancellationToken = default)
        {
            if (shouldFail)
                throw new ClassifierUnavailableException("Unavailable", HttpStatusCode.ServiceUnavailable);

            var active = Interlocked.Increment(ref activeCalls);
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, active);
            try
            {
                if (delay is { } duration) await Task.Delay(duration, cancellationToken);
                return CreateResponse();
            }
            finally
            {
                Interlocked.Decrement(ref activeCalls);
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private static ClassifierWellnessResponse CreateResponse()
    {
        var item = new ClassifierWellnessBreakdownItem
        {
            Score = 10,
            MaxScore = 10,
            Availability = ClassifierWellnessDimensionAvailability.Available,
            Included = true,
            ReasonCodes = [ClassifierWellnessReasonCode.ActivityTargetMet]
        };
        return new ClassifierWellnessResponse
        {
            WellnessScore = 82,
            Band = ClassifierWellnessBand.Excellent,
            BandLabel = "Excellent",
            ScoreStatus = ClassifierWellnessScoreStatus.Complete,
            DataCoverage = 1,
            CalculationVersion = "1.0.0",
            EvaluatedAt = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            Trend = ClassifierWellnessTrendDirection.Stable,
            Breakdown = new ClassifierWellnessBreakdown
            {
                Activity = item,
                Sleep = item,
                Diet = item,
                Symptoms = item,
                PreventiveCare = item,
                Baseline = item
            },
            Narrative = "Stable.",
            Recommendations = [],
            Disclaimer = "Not veterinary advice."
        };
    }
}
