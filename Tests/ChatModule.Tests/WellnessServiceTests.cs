using System.Net;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.WellnessModule.Domain;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class WellnessServiceTests
{
    [Fact]
    public async Task EvaluateAsync_PersistsValidClassifierResponse()
    {
        await using var db = CreateContext();
        var petId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await AddPetAsync(db, petId, userId);
        var service = CreateService(db, new RecordingClassifier());

        var result = await service.EvaluateAsync(
            petId, userId, null, TestContext.Current.CancellationToken);

        Assert.Equal(82, result.WellnessScore);
        Assert.Equal(ClassifierWellnessBand.Excellent, result.Band);
        Assert.Equal(ClassifierWellnessScoreStatus.Complete, result.ScoreStatus);
        Assert.Equal(ClassifierWellnessReasonCode.ActivityTargetMet, result.States.Activity);
        Assert.Equal("Stable.", result.Narrative);
        Assert.Equal(["Keep the routine."], result.Recommendations);
        Assert.Collection(
            result.ReminderSuggestions,
            item =>
            {
                Assert.Equal(ClassifierWellnessReminderType.Feeding, item.Type);
                Assert.Equal("Keep meal times consistent.", item.Text);
            },
            item =>
            {
                Assert.Equal(ClassifierWellnessReminderType.Bathing, item.Type);
                Assert.Equal("Track grooming care.", item.Text);
            });
        var stored = Assert.Single(await db.PetWellnessAssessments.ToListAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("1.0.0", stored.CalculationVersion);
        Assert.Contains("wellnessScore", stored.ResponseJson);
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotPersistFailedCalculation()
    {
        await using var db = CreateContext();
        var petId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await AddPetAsync(db, petId, userId);
        var service = CreateService(db, new RecordingClassifier(shouldFail: true));

        await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            service.EvaluateAsync(
                petId, userId, null, TestContext.Current.CancellationToken));

        Assert.Empty(await db.PetWellnessAssessments.ToListAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAsync_SerializesConcurrentCallsAndAllowsOnlyOneInitialEvaluation()
    {
        await using var db = CreateContext();
        var classifier = new RecordingClassifier(delay: TimeSpan.FromMilliseconds(75));
        var service = CreateService(db, classifier);
        var petId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await AddPetAsync(db, petId, userId);

        await Assert.ThrowsAsync<WellnessEvaluationAlreadyExistsException>(() => Task.WhenAll(
            service.EvaluateAsync(petId, userId, null, TestContext.Current.CancellationToken),
            service.EvaluateAsync(petId, userId, null, TestContext.Current.CancellationToken)));

        Assert.Equal(1, classifier.MaximumConcurrentCalls);
        Assert.Equal(1, await db.PetWellnessAssessments.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAsync_WhenInformationIsInsufficient_DoesNotPersistResult()
    {
        await using var db = CreateContext();
        var petId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await AddPetAsync(db, petId, userId);
        var service = CreateService(db, new RecordingClassifier(insufficientData: true));

        await Assert.ThrowsAsync<WellnessInsufficientDataException>(() =>
            service.EvaluateAsync(petId, userId, null, TestContext.Current.CancellationToken));

        Assert.Empty(await db.PetWellnessAssessments.ToListAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAsync_WhenInitialEvaluationExists_RejectsDuplicate()
    {
        await using var db = CreateContext();
        var petId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        await AddPetAsync(db, petId, userId);
        await AddAssessmentAsync(db, petId, now - TimeSpan.FromDays(10));
        var service = CreateService(db, new RecordingClassifier(), now);

        await Assert.ThrowsAsync<WellnessEvaluationAlreadyExistsException>(() =>
            service.EvaluateAsync(petId, userId, null, TestContext.Current.CancellationToken));
    }

    private static WellnessService CreateService(
        AppDbContext db,
        IClassifierClient classifier,
        DateTimeOffset? now = null) =>
        new(
            db,
            new StubAggregator(),
            classifier,
            new WellnessEvaluationLock(),
            new FixedTimeProvider(now ?? new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero)));

    private static async Task AddPetAsync(AppDbContext db, Guid petId, Guid userId)
    {
        db.Pets.Add(new Pet { Id = petId, UserId = userId, Name = "Milo" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task AddAssessmentAsync(
        AppDbContext db,
        Guid petId,
        DateTimeOffset createdAt)
    {
        db.PetWellnessAssessments.Add(new PetWellnessAssessment
        {
            PetId = petId,
            ScoreStatus = "Complete",
            DataCoverage = 1,
            CalculationVersion = "1.0.0",
            EvaluatedAt = createdAt.UtcDateTime,
            ResponseJson = "{}",
            CreatedAt = createdAt.UtcDateTime
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

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

    private sealed class RecordingClassifier(
        bool shouldFail = false,
        TimeSpan? delay = null,
        bool insufficientData = false)
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
                var response = CreateResponse();
                return insufficientData
                    ? response with
                    {
                        WellnessScore = null,
                        Band = null,
                        BandLabel = null,
                        ScoreStatus = ClassifierWellnessScoreStatus.InsufficientData
                    }
                    : response;
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
            Recommendations = ["Keep the routine."],
            Reminders =
            [
                new ClassifierWellnessReminder
                {
                    Reminder = ClassifierWellnessReminderType.Feeding,
                    Text = "Keep meal times consistent."
                }
            ],
            TrackingRecommendations =
            [
                new ClassifierWellnessTrackingRecommendation
                {
                    Dimension = ClassifierWellnessDimension.RoutineCare,
                    Text = "Track grooming care.",
                    RequiredInputs = ["routineCare"],
                    SuggestedReminderTypes = [ClassifierWellnessReminderType.Bathing]
                }
            ],
            Disclaimer = "Not veterinary advice."
        };
    }
}
