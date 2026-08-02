using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NutritionModule.Domain;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.Tests;

public class NutritionAnalysisServiceTests
{
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private sealed record Harness(
        NutritionAnalysisService Service,
        FakeNutritionAnalysisRepository Analyses,
        FakeClassifierClient Classifier,
        FakeFeedingLogRepository Feeding,
        FakePetRepository Pets,
        FakeNutritionGoalRepository Goals);

    private Harness BuildHarness(
        ClassifierFeedingSummaryResponse? response = null,
        Exception? classifierException = null,
        bool petExists = true,
        decimal? weightKg = 12.4m,
        DateTime? birthDate = null,
        int? dailyCalorieTarget = null)
    {
        var feeding = new FakeFeedingLogRepository();
        var pets = new FakePetRepository
        {
            PetExists = petExists,
            Pet = new Pet
            {
                Id = _petId,
                UserId = _userId,
                Name = "Buddy",
                Species = AnimalSpecies.Dog,
                Breed = "Beagle",
                WeightKg = weightKg,
                BirthDate = birthDate ?? DateTime.UtcNow.AddDays(-365)
            }
        };
        var analyses = new FakeNutritionAnalysisRepository();
        var classifier = new FakeClassifierClient(response, classifierException);
        var goals = new FakeNutritionGoalRepository
        {
            Goal = dailyCalorieTarget is null
                ? null
                : new NutritionGoal { PetId = _petId, DailyCalorieTarget = dailyCalorieTarget }
        };

        var service = new NutritionAnalysisService(analyses, pets, feeding, goals, classifier);

        return new Harness(service, analyses, classifier, feeding, pets, goals);
    }

    private FeedingLog Log(int? calories, string? foodName = null, FoodType foodType = FoodType.DryFood) => new()
    {
        PetId = _petId,
        FedAt = DateTime.UtcNow,
        FoodType = foodType,
        FoodName = foodName,
        ApproxCalories = calories
    };

    private NutritionAnalysis StoredAnalysis(DateTime createdAt, FeedingStatus status) => new()
    {
        PetId = _petId,
        Date = DateOnly.FromDateTime(createdAt),
        Status = status,
        TargetCalories = 600m,
        ActualCalories = 500m,
        DeviationPct = -16.67m,
        Disclaimer = "disclaimer",
        CreatedAt = createdAt
    };

    private ClassifierFeedingSummaryResponse ResponseFor(
        ClassifierFeedingStatus status,
        decimal target = 600m,
        decimal actual = 480m,
        decimal deviationPct = -20m) => new()
        {
            Results =
            [
                new ClassifierFeedingSummaryResult
                {
                    PetId = _petId.ToString("D"),
                    Status = status,
                    TargetCalories = target,
                    ActualCalories = actual,
                    DeviationPct = deviationPct
                }
            ],
            Disclaimer = "This guidance does not replace a veterinary examination."
        };

    // ----- ownership and validation -----

    [Fact]
    public async Task Analyze_WhenPetDoesNotBelongToUser_ThrowsWithoutCallingClassifier()
    {
        var harness = BuildHarness(petExists: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Pet not found", ex.Message);
        Assert.Empty(harness.Classifier.Requests);
        Assert.Empty(harness.Analyses.Stored);
    }

    [Fact]
    public async Task GetRecent_WhenPetDoesNotBelongToUser_Throws()
    {
        var harness = BuildHarness(petExists: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.GetRecentAsync(_petId, _userId));
    }

    [Fact]
    public async Task Analyze_WhenOffsetOutOfRange_ThrowsWithoutCallingClassifier()
    {
        var harness = BuildHarness();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 841, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Classifier.Requests);
    }

    /// <summary>
    /// The route derives the calorie target from body weight, so there is
    /// nothing to grade without one — and the classifier would answer 422.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public async Task Analyze_WhenWeightIsMissingOrOutOfRange_ThrowsWithoutCallingClassifier(int? weight)
    {
        var harness = BuildHarness(weightKg: weight);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Classifier.Requests);
        Assert.Empty(harness.Analyses.Stored);
    }

    // ----- request building -----

    [Fact]
    public async Task Analyze_SendsTheSinglePetWithItsContextAndProducts()
    {
        var harness = BuildHarness();
        harness.Feeding.Logs = [Log(300, "Chicken kibble"), Log(180, "Salmon treat")];

        await harness.Service.AnalyzeAsync(
            _petId, _userId, new DateOnly(2026, 7, 15), 120, cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(harness.Classifier.Requests);
        var pet = Assert.Single(request.Pets);

        Assert.Equal(_petId.ToString("D"), pet.PetId);
        Assert.Equal(ClassifierPetType.Dog, pet.Species);
        Assert.Equal("Beagle", pet.Breed);
        Assert.Equal(12.4m, pet.WeightKg);
        Assert.Equal(12, pet.AgeMonths);

        // Ordered by calories, largest first.
        Assert.Equal(
            new[] { ("Chicken kibble", 300m), ("Salmon treat", 180m) },
            pet.Products.Select(p => (p.Name, p.Calories)).ToArray());
    }

    [Fact]
    public async Task Analyze_ReadsOnlyTheRequestedLocalDay()
    {
        var harness = BuildHarness();

        await harness.Service.AnalyzeAsync(
            _petId, _userId, new DateOnly(2026, 7, 15), 120, cancellationToken: TestContext.Current.CancellationToken);

        // Local midnight in UTC+2 is 22:00 the previous day, UTC.
        Assert.Equal(_petId, harness.Feeding.RequestedPetId);
        Assert.Equal(
            new DateTime(2026, 7, 14, 22, 0, 0, DateTimeKind.Utc),
            harness.Feeding.RequestedStartUtc);
        Assert.Equal(
            new DateTime(2026, 7, 15, 22, 0, 0, DateTimeKind.Utc),
            harness.Feeding.RequestedEndUtc);
    }

    [Fact]
    public async Task Analyze_MergesLogsOfTheSameFoodAndFallsBackToTheFoodType()
    {
        var harness = BuildHarness();
        harness.Feeding.Logs =
        [
            Log(200, "Chicken kibble"),
            Log(150, "Chicken kibble"),
            Log(90, foodName: null, foodType: FoodType.Treat),
            Log(null, "Chicken kibble")
        ];

        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);
        Assert.Equal(
            new[] { ("Chicken kibble", 350m), ("Treat", 90m) },
            pet.Products.Select(p => (p.Name, p.Calories)).ToArray());
    }

    [Fact]
    public async Task Analyze_WhenNothingWasLogged_SendsNoProducts()
    {
        var harness = BuildHarness();

        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);
        Assert.Empty(pet.Products);
    }

    /// <summary>
    /// The route accepts at most 100 products, so the tail is merged rather than
    /// dropped — the graded calorie total has to stay intact.
    /// </summary>
    [Fact]
    public async Task Analyze_WhenOverTheProductCap_MergesTheTailWithoutLosingCalories()
    {
        var harness = BuildHarness();
        harness.Feeding.Logs = [.. Enumerable.Range(1, 150).Select(i => Log(i, $"Food {i}"))];

        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);
        Assert.Equal(100, pet.Products.Count);
        Assert.Equal((decimal)Enumerable.Range(1, 150).Sum(), pet.Products.Sum(p => p.Calories));
        Assert.Equal("Other foods", pet.Products[^1].Name);
    }

    [Fact]
    public async Task Analyze_WhenBirthDateIsAbsurdlyOld_ClampsAgeToTheRouteMaximum()
    {
        var harness = BuildHarness(birthDate: DateTime.UtcNow.AddYears(-80));

        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);
        Assert.Equal(600, pet.AgeMonths);
    }

    // ----- caller-supplied overrides -----

    [Fact]
    public async Task Analyze_WhenTheBodySuppliesContext_ItOverridesTheStoredPet()
    {
        var harness = BuildHarness();

        await harness.Service.AnalyzeAsync(
            _petId,
            _userId,
            null,
            0,
            new NutritionAnalysisRequestDto
            {
                Species = AnimalSpecies.Cat,
                Breed = "Siamese",
                WeightKg = 4.2m,
                AgeMonths = 30
            },
            TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);
        Assert.Equal(ClassifierPetType.Cat, pet.Species);
        Assert.Equal("Siamese", pet.Breed);
        Assert.Equal(4.2m, pet.WeightKg);
        Assert.Equal(30, pet.AgeMonths);
    }

    /// <summary>
    /// A body that sets only one field must not blank out the rest — the pet's
    /// own data still fills every gap.
    /// </summary>
    [Fact]
    public async Task Analyze_WhenTheBodyIsPartial_UnsetFieldsStillComeFromThePet()
    {
        var harness = BuildHarness();

        await harness.Service.AnalyzeAsync(
            _petId,
            _userId,
            null,
            0,
            new NutritionAnalysisRequestDto { WeightKg = 15m },
            TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);
        Assert.Equal(15m, pet.WeightKg);
        Assert.Equal(ClassifierPetType.Dog, pet.Species);
        Assert.Equal("Beagle", pet.Breed);
        Assert.Equal(12, pet.AgeMonths);
    }

    [Fact]
    public async Task Analyze_WhenTheBodySuppliesProducts_TheFeedingLogsAreNotRead()
    {
        var harness = BuildHarness();
        harness.Feeding.Logs = [Log(999, "Should be ignored")];

        var result = await harness.Service.AnalyzeAsync(
            _petId,
            _userId,
            null,
            0,
            new NutritionAnalysisRequestDto
            {
                Products =
                [
                    new NutritionAnalysisProductDto { Name = "Chicken kibble", Calories = 300m },
                    new NutritionAnalysisProductDto { Name = "Salmon treat", Calories = 180m }
                ]
            },
            TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);

        // Passed through in the order given, not reordered by calories the way
        // logs are, and the ignored log is nowhere in the request.
        Assert.Equal(
            new[] { ("Chicken kibble", 300m), ("Salmon treat", 180m) },
            pet.Products.Select(p => (p.Name, p.Calories)).ToArray());

        Assert.Null(harness.Feeding.RequestedPetId);
        Assert.Equal(2, result.MealCount);
    }

    /// <summary>
    /// An empty array is a deliberate "ate nothing", which is different from
    /// omitting the property and falling back to the day's logs.
    /// </summary>
    [Fact]
    public async Task Analyze_WhenTheBodySuppliesNoProducts_GradesTheDayAsEmpty()
    {
        var harness = BuildHarness();
        harness.Feeding.Logs = [Log(300, "Chicken kibble")];

        var result = await harness.Service.AnalyzeAsync(
            _petId,
            _userId,
            null,
            0,
            new NutritionAnalysisRequestDto { Products = [] },
            TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);
        Assert.Empty(pet.Products);
        Assert.Null(harness.Feeding.RequestedPetId);
        Assert.Equal(0, result.MealCount);
    }

    /// <summary>
    /// The supplied weight is what the grading needs, so a pet with none
    /// recorded can still be analysed.
    /// </summary>
    [Fact]
    public async Task Analyze_WhenThePetHasNoWeight_TheSuppliedOneIsEnough()
    {
        var harness = BuildHarness(weightKg: null);

        await harness.Service.AnalyzeAsync(
            _petId,
            _userId,
            null,
            0,
            new NutritionAnalysisRequestDto { WeightKg = 9.5m },
            TestContext.Current.CancellationToken);

        var pet = Assert.Single(Assert.Single(harness.Classifier.Requests).Pets);
        Assert.Equal(9.5m, pet.WeightKg);
    }

    /// <summary>
    /// Model validation catches this at the edge, but the service is the last
    /// line before the classifier answers 422.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public async Task Analyze_WhenTheSuppliedWeightIsOutOfRange_ThrowsWithoutCallingClassifier(int weight)
    {
        var harness = BuildHarness();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.AnalyzeAsync(
                _petId,
                _userId,
                null,
                0,
                new NutritionAnalysisRequestDto { WeightKg = weight },
                TestContext.Current.CancellationToken));

        Assert.Empty(harness.Classifier.Requests);
        Assert.Empty(harness.Analyses.Stored);
    }

    // ----- nutrition goal as the target -----

    /// <summary>
    /// The classifier route takes no target input, so a user-set goal can only
    /// be applied to its answer afterwards. Target, deviation and status must
    /// move together or the stored row contradicts itself.
    /// </summary>
    [Fact]
    public async Task Analyze_WhenAGoalSetsACalorieTarget_ItReplacesTheClassifierTarget()
    {
        var harness = BuildHarness(
            ResponseFor(
                ClassifierFeedingStatus.OnTarget,
                target: 740.1m,
                actual: 480m,
                deviationPct: -35.1m),
            dailyCalorieTarget: 600);

        var result = await harness.Service.AnalyzeAsync(
            _petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(600m, result.TargetCalories);
        Assert.Equal(480m, result.ActualCalories);

        // (480 - 600) / 600 = -20%, which lands in the UNDER_TARGET band and
        // replaces the ON_TARGET the classifier reported for its own target.
        Assert.Equal(-20m, result.DeviationPct);
        Assert.Equal(FeedingStatus.UnderTarget, result.Status);
    }

    [Fact]
    public async Task Analyze_WhenNoGoalExists_TheClassifierTargetIsKept()
    {
        var harness = BuildHarness(
            ResponseFor(
                ClassifierFeedingStatus.UnderTarget,
                target: 740.1m,
                actual: 480m,
                deviationPct: -35.1m));

        var result = await harness.Service.AnalyzeAsync(
            _petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(740.1m, result.TargetCalories);
        Assert.Equal(-35.1m, result.DeviationPct);
        Assert.Equal(FeedingStatus.UnderTarget, result.Status);
    }

    /// <summary>
    /// A goal that sets no calorie target, or sets it to zero, has nothing to
    /// grade against — and zero would divide. The classifier's own target
    /// stands in both cases.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Analyze_WhenTheGoalHasNoUsableTarget_TheClassifierTargetIsKept(int? target)
    {
        var harness = BuildHarness(
            ResponseFor(ClassifierFeedingStatus.UnderTarget, target: 740.1m, actual: 480m, deviationPct: -35.1m),
            dailyCalorieTarget: target);
        harness.Goals.Goal = new NutritionGoal { PetId = _petId, DailyCalorieTarget = target };

        var result = await harness.Service.AnalyzeAsync(
            _petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(740.1m, result.TargetCalories);
        Assert.Equal(-35.1m, result.DeviationPct);
    }

    [Theory]
    [InlineData(600, 600, FeedingStatus.OnTarget)]        //    0%
    [InlineData(600, 660, FeedingStatus.OnTarget)]        //  +10%, band edge
    [InlineData(600, 540, FeedingStatus.OnTarget)]        //  -10%, band edge
    [InlineData(600, 661, FeedingStatus.OverTarget)]      // +10.17%
    [InlineData(600, 539, FeedingStatus.UnderTarget)]     // -10.17%
    [InlineData(600, 900, FeedingStatus.ExtremeOverTarget)]  //  +50%, band edge
    [InlineData(600, 300, FeedingStatus.ExtremeUnderTarget)] //  -50%, band edge
    [InlineData(600, 0, FeedingStatus.ExtremeUnderTarget)]   // -100%
    public async Task Analyze_GradesTheGoalDeviationIntoTheRightBand(
        int goalTarget, int actual, FeedingStatus expected)
    {
        var harness = BuildHarness(
            ResponseFor(ClassifierFeedingStatus.OnTarget, actual: actual),
            dailyCalorieTarget: goalTarget);

        var result = await harness.Service.AnalyzeAsync(
            _petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Analyze_WhenTheClassifierFails_TheGoalIsNotRead()
    {
        var harness = BuildHarness(
            classifierException: new ClassifierUnavailableException("down"),
            dailyCalorieTarget: 600);

        await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            harness.Service.AnalyzeAsync(
                _petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Analyses.Stored);
    }

    // ----- persistence / mapping -----

    [Fact]
    public async Task Analyze_StoresTheGradedFiguresAndTheMealSnapshot()
    {
        var harness = BuildHarness(
            ResponseFor(ClassifierFeedingStatus.ExtremeUnderTarget, target: 700m, actual: 120m, deviationPct: -82.86m));
        harness.Feeding.Logs = [Log(120, "Chicken kibble")];

        var result = await harness.Service.AnalyzeAsync(
            _petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(FeedingStatus.ExtremeUnderTarget, result.Status);
        Assert.Equal(700m, result.TargetCalories);
        Assert.Equal(120m, result.ActualCalories);
        Assert.Equal(-82.86m, result.DeviationPct);
        Assert.Equal("This guidance does not replace a veterinary examination.", result.Disclaimer);

        // Snapshot of what was analysed, so a later feeding log cannot silently
        // change what the stored verdict was based on.
        Assert.Equal(1, result.MealCount);

        var stored = Assert.Single(harness.Analyses.Stored);
        Assert.Equal(FeedingStatus.ExtremeUnderTarget, stored.Status);
        Assert.Equal(_petId, stored.PetId);
    }

    [Theory]
    [InlineData(ClassifierFeedingStatus.ExtremeUnderTarget, FeedingStatus.ExtremeUnderTarget)]
    [InlineData(ClassifierFeedingStatus.UnderTarget, FeedingStatus.UnderTarget)]
    [InlineData(ClassifierFeedingStatus.OnTarget, FeedingStatus.OnTarget)]
    [InlineData(ClassifierFeedingStatus.OverTarget, FeedingStatus.OverTarget)]
    [InlineData(ClassifierFeedingStatus.ExtremeOverTarget, FeedingStatus.ExtremeOverTarget)]
    public async Task Analyze_MapsEveryClassifierStatus(
        ClassifierFeedingStatus wire, FeedingStatus expected)
    {
        var harness = BuildHarness(ResponseFor(wire));

        var result = await harness.Service.AnalyzeAsync(
            _petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Analyze_StoresTheAnalysedLocalDayAndOffset()
    {
        var harness = BuildHarness();

        var result = await harness.Service.AnalyzeAsync(
            _petId, _userId, new DateOnly(2026, 7, 15), 120, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new DateOnly(2026, 7, 15), result.Date);
        Assert.Equal(120, result.UtcOffsetMinutes);
    }

    /// <summary>
    /// A batch response that grades some other pet cannot answer this request,
    /// and is reported as a broken contract rather than stored.
    /// </summary>
    [Fact]
    public async Task Analyze_WhenResultsHoldAnotherPet_ThrowsAndStoresNothing()
    {
        var harness = BuildHarness(new ClassifierFeedingSummaryResponse
        {
            Results = [FakeClassifierClient.DefaultResult(Guid.NewGuid().ToString("D"))],
            Disclaimer = "disclaimer"
        });

        var ex = await Assert.ThrowsAsync<ClassifierInvalidResponseException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("results holds no entry for the requested petId", ex.ValidationReason);
        Assert.Empty(harness.Analyses.Stored);
    }

    [Fact]
    public async Task Analyze_WhenClassifierIsRateLimited_StoresNothing()
    {
        var harness = BuildHarness(
            classifierException: new ClassifierRateLimitedException("Busy.", "rate_limit_exceeded"));

        await Assert.ThrowsAsync<ClassifierRateLimitedException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Analyses.Stored);
    }

    [Fact]
    public async Task Analyze_WhenClassifierFails_StoresNothing()
    {
        var harness = BuildHarness(
            classifierException: new ClassifierUnavailableException("Classifier is unavailable."));

        await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Analyses.Stored);
    }

    // ----- retention -----

    [Fact]
    public async Task Analyze_KeepsOnlyTheTwoMostRecentAnalyses()
    {
        var harness = BuildHarness();
        var oldest = StoredAnalysis(DateTime.UtcNow.AddDays(-2), FeedingStatus.ExtremeOverTarget);
        var older = StoredAnalysis(DateTime.UtcNow.AddDays(-1), FeedingStatus.OnTarget);
        harness.Analyses.Stored.AddRange([oldest, older]);

        var result = await harness.Service.AnalyzeAsync(
            _petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, harness.Analyses.Stored.Count);
        Assert.DoesNotContain(oldest, harness.Analyses.Stored);
        Assert.Contains(older, harness.Analyses.Stored);
        Assert.Contains(harness.Analyses.Stored, a => a.Id == result.Id);
    }

    [Fact]
    public async Task Analyze_DoesNotTouchAnotherPetsAnalyses()
    {
        var harness = BuildHarness();
        var otherPets = new NutritionAnalysis
        {
            PetId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = FeedingStatus.OnTarget,
            TargetCalories = 500m,
            ActualCalories = 500m,
            DeviationPct = 0m,
            Disclaimer = "disclaimer",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        harness.Analyses.Stored.AddRange([
            otherPets,
            StoredAnalysis(DateTime.UtcNow.AddDays(-2), FeedingStatus.ExtremeOverTarget),
            StoredAnalysis(DateTime.UtcNow.AddDays(-1), FeedingStatus.OnTarget)]);

        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(otherPets, harness.Analyses.Stored);
    }

    // ----- history -----

    [Fact]
    public async Task GetRecent_ReturnsLatestAndPreviousNewestFirst()
    {
        var harness = BuildHarness();
        var previous = StoredAnalysis(DateTime.UtcNow.AddDays(-1), FeedingStatus.OnTarget);
        var latest = StoredAnalysis(DateTime.UtcNow, FeedingStatus.UnderTarget);
        harness.Analyses.Stored.AddRange([previous, latest]);

        var history = await harness.Service.GetRecentAsync(_petId, _userId);

        Assert.Equal(latest.Id, history.Latest?.Id);
        Assert.Equal(previous.Id, history.Previous?.Id);
    }

    [Fact]
    public async Task GetRecent_WhenNoneStored_ReturnsNulls()
    {
        var harness = BuildHarness();

        var history = await harness.Service.GetRecentAsync(_petId, _userId);

        Assert.Null(history.Latest);
        Assert.Null(history.Previous);
    }

    [Fact]
    public async Task GetRecent_AfterFirstAnalysis_HasNoPrevious()
    {
        var harness = BuildHarness();
        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, cancellationToken: TestContext.Current.CancellationToken);

        var history = await harness.Service.GetRecentAsync(_petId, _userId);

        Assert.NotNull(history.Latest);
        Assert.Null(history.Previous);
    }
}
