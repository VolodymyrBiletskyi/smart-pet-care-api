using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NutritionModule.Domain;
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
        FakeNutritionGoalRepository Goals,
        FakeFeedingLogRepository Feeding,
        FakePetRepository Pets);

    private Harness BuildHarness(
        ClassifierNutritionResponse? response = null,
        Exception? classifierException = null,
        bool petExists = true,
        double calorieRatio = 1.0,
        string? chatAnswer = null)
    {
        var goals = new FakeNutritionGoalRepository();
        var feeding = new FakeFeedingLogRepository();
        var reminders = new FakeReminderRepository();
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
                WeightKg = 12.4m,
                BirthDate = DateTime.UtcNow.AddDays(-365)
            }
        };
        var analyses = new FakeNutritionAnalysisRepository();
        var classifier = new FakeClassifierClient(
            response,
            classifierException,
            FakeClassifierClient.DefaultWellness(calorieRatio),
            FakeClassifierClient.DefaultChat(chatAnswer));

        var nutritionService = new NutritionService(goals, pets, feeding, reminders);
        var service = new NutritionAnalysisService(nutritionService, analyses, pets, classifier);

        return new Harness(service, analyses, classifier, goals, feeding, pets);
    }

    private FeedingLog Log(int? calories, decimal? amount, PortionUnit? unit) => new()
    {
        PetId = _petId,
        FedAt = DateTime.UtcNow,
        FoodType = FoodType.DryFood,
        ApproxCalories = calories,
        PortionAmount = amount,
        PortionUnit = unit
    };

    private NutritionAnalysis StoredAnalysis(DateTime createdAt, NutritionGrade grade) => new()
    {
        PetId = _petId,
        Date = DateOnly.FromDateTime(createdAt),
        Grade = grade,
        Score = 50,
        Summary = "stored",
        Advice = ["stored advice"],
        Disclaimer = "disclaimer",
        CreatedAt = createdAt
    };

    // ----- ownership -----

    [Fact]
    public async Task Analyze_WhenPetDoesNotBelongToUser_ThrowsWithoutCallingClassifier()
    {
        var harness = BuildHarness(petExists: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken));

        Assert.Equal("Pet not found", ex.Message);
        Assert.Empty(harness.Classifier.WellnessRequests);
        Assert.Empty(harness.Classifier.ChatRequests);
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
            harness.Service.AnalyzeAsync(_petId, _userId, null, 841, TestContext.Current.CancellationToken));

        Assert.Empty(harness.Classifier.WellnessRequests);
        Assert.Empty(harness.Classifier.ChatRequests);
    }

    // ----- request building -----

    [Fact]
    public async Task Analyze_SendsPetContextAndFeedingFiguresToWellness()
    {
        var harness = BuildHarness();
        harness.Feeding.Logs = [Log(300, 120m, PortionUnit.Gram), Log(180, 90m, PortionUnit.Gram)];

        await harness.Service.AnalyzeAsync(_petId, _userId, new DateOnly(2026, 7, 15), 120, TestContext.Current.CancellationToken);

        var request = Assert.Single(harness.Classifier.WellnessRequests);
        Assert.Equal(ClassifierPetType.Dog, request.Pet.Species);
        Assert.Equal("Beagle", request.Pet.Breed);
        Assert.Equal(12.4m, request.Pet.WeightKg);
        Assert.Equal(12, request.Pet.AgeMonths);
        Assert.Equal(2, request.Feeding?.AvgMealsPerDay);
        Assert.Equal(480, request.Feeding?.AvgCaloriesPerDay);
        Assert.Equal("2026-07-15", request.EvaluationWindow?.StartDate);
        Assert.Equal("2026-07-15", request.EvaluationWindow?.EndDate);
    }

    /// <summary>
    /// The classifier rejects a consistency window longer than a week, and one
    /// analysed day is one day of history either way.
    /// </summary>
    [Fact]
    public async Task Analyze_SendsASingleDayConsistencyWindow()
    {
        var harness = BuildHarness();

        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken);

        var request = Assert.Single(harness.Classifier.WellnessRequests);
        Assert.Equal(1, request.Feeding?.ConsistencyDays);
        Assert.InRange(
            request.Feeding!.ConsistencyDays,
            1,
            ClassifierWellnessFeeding.MaximumConsistencyDays);
    }

    [Fact]
    public async Task Analyze_SendsTheDaysFiguresAndTheGoalToChatAsOneTurn()
    {
        var harness = BuildHarness();
        harness.Feeding.Logs = [Log(300, 120m, PortionUnit.Gram), Log(180, 90m, PortionUnit.Gram)];
        harness.Goals.Goal = new NutritionGoal
        {
            PetId = _petId,
            DailyCalorieTarget = 600,
            DailyPortionTarget = 250m,
            PortionUnit = PortionUnit.Gram,
            MealsPerDay = 3
        };

        await harness.Service.AnalyzeAsync(_petId, _userId, new DateOnly(2026, 7, 15), 120, TestContext.Current.CancellationToken);

        var request = Assert.Single(harness.Classifier.ChatRequests);
        Assert.Equal(ClassifierPetType.Dog, request.PetType);
        Assert.Null(request.SessionId);

        var message = Assert.Single(request.Messages);
        Assert.Equal(ClassifierChatRole.User, message.Role);
        Assert.Contains("2026-07-15", message.Content);
        Assert.Contains("Meals logged: 2", message.Content);
        Assert.Contains("Total calories: 480", message.Content);
        Assert.Contains("210 Gram", message.Content);
        Assert.Contains("600 kcal", message.Content);
        Assert.Contains("Calories remaining against the goal: 120", message.Content);
    }

    [Fact]
    public async Task Analyze_WhenNoGoalIsSet_ChatPromptCarriesNoGoalOrComparison()
    {
        var harness = BuildHarness();

        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken);

        var message = Assert.Single(Assert.Single(harness.Classifier.ChatRequests).Messages);
        Assert.DoesNotContain("Daily goal:", message.Content);
        Assert.DoesNotContain("remaining against the goal", message.Content);
    }

    // ----- persistence / mapping -----

    [Fact]
    public async Task Analyze_GradesOnTheCalorieRatioAndTakesProseFromChat()
    {
        // 0.6 of target is 40 points off, so it grades D.
        var harness = BuildHarness(
            calorieRatio: 0.6,
            chatAnswer: "Well under target.\n- Increase portion size.\n- Log the evening meal.");
        harness.Feeding.Logs = [Log(120, 50m, PortionUnit.Gram)];

        var result = await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken);

        Assert.Equal(NutritionGrade.D, result.Grade);
        Assert.Equal(40, result.Score);
        Assert.Equal("Well under target.", result.Summary);
        Assert.Equal(["Increase portion size.", "Log the evening meal."], result.Advice);
        Assert.Equal("This guidance does not replace a veterinary examination.", result.Disclaimer);

        // Snapshot of what was analysed, so a later feeding log cannot silently
        // change what the stored verdict was based on.
        Assert.Equal(1, result.MealCount);
        Assert.Equal(120, result.TotalCalories);

        var stored = Assert.Single(harness.Analyses.Stored);
        Assert.Equal(NutritionGrade.D, stored.Grade);
        Assert.Equal(_petId, stored.PetId);
    }

    [Theory]
    [InlineData(1.0, 100, NutritionGrade.A)]   // exactly on target
    [InlineData(0.95, 95, NutritionGrade.A)]
    [InlineData(1.106, 89, NutritionGrade.B)]  // the ratio observed for a real under-fed day
    [InlineData(0.8, 80, NutritionGrade.B)]
    [InlineData(1.35, 65, NutritionGrade.C)]
    [InlineData(0.5, 50, NutritionGrade.D)]
    [InlineData(0.2304, 23, NutritionGrade.F)] // severely under
    [InlineData(2.765, 0, NutritionGrade.F)]   // severely over
    [InlineData(3.0, 0, NutritionGrade.F)]     // clamped, never negative
    public async Task Analyze_MapsCalorieRatioOntoScoreAndGrade(
        double ratio, int expectedScore, NutritionGrade expectedGrade)
    {
        var harness = BuildHarness(calorieRatio: ratio);

        var result = await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken);

        Assert.Equal(expectedScore, result.Score);
        Assert.Equal(expectedGrade, result.Grade);
    }

    /// <summary>
    /// The chat route returns prose, so an answer with no list still has to
    /// produce a usable summary rather than being split into fragments.
    /// </summary>
    [Fact]
    public async Task Analyze_WhenChatReturnsNoBullets_KeepsTheWholeAnswerAsTheSummary()
    {
        var harness = BuildHarness(
            chatAnswer: "Your Beagle is slightly under their calorie goal for the day.");

        var result = await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken);

        Assert.Equal("Your Beagle is slightly under their calorie goal for the day.", result.Summary);
        Assert.Empty(result.Advice);
    }

    [Fact]
    public async Task Analyze_WhenChatFails_StoresNothing()
    {
        var harness = BuildHarness(
            classifierException: new ClassifierRateLimitedException("Busy.", "rate_limit_exceeded"));

        await Assert.ThrowsAsync<ClassifierRateLimitedException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken));

        Assert.Empty(harness.Analyses.Stored);
    }

    [Fact]
    public async Task Analyze_WhenClassifierFails_StoresNothing()
    {
        var harness = BuildHarness(
            classifierException: new ClassifierUnavailableException("Classifier is unavailable."));

        await Assert.ThrowsAsync<ClassifierUnavailableException>(() =>
            harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken));

        Assert.Empty(harness.Analyses.Stored);
    }

    // ----- retention -----

    [Fact]
    public async Task Analyze_KeepsOnlyTheTwoMostRecentAnalyses()
    {
        var harness = BuildHarness();
        var oldest = StoredAnalysis(DateTime.UtcNow.AddDays(-2), NutritionGrade.F);
        var older = StoredAnalysis(DateTime.UtcNow.AddDays(-1), NutritionGrade.C);
        harness.Analyses.Stored.AddRange([oldest, older]);

        var result = await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken);

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
            Grade = NutritionGrade.A,
            Score = 95,
            Summary = "other pet",
            Disclaimer = "disclaimer",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        harness.Analyses.Stored.AddRange([
            otherPets,
            StoredAnalysis(DateTime.UtcNow.AddDays(-2), NutritionGrade.F),
            StoredAnalysis(DateTime.UtcNow.AddDays(-1), NutritionGrade.C)]);

        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken);

        Assert.Contains(otherPets, harness.Analyses.Stored);
    }

    // ----- history -----

    [Fact]
    public async Task GetRecent_ReturnsLatestAndPreviousNewestFirst()
    {
        var harness = BuildHarness();
        var previous = StoredAnalysis(DateTime.UtcNow.AddDays(-1), NutritionGrade.C);
        var latest = StoredAnalysis(DateTime.UtcNow, NutritionGrade.A);
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
        await harness.Service.AnalyzeAsync(_petId, _userId, null, 0, TestContext.Current.CancellationToken);

        var history = await harness.Service.GetRecentAsync(_petId, _userId);

        Assert.NotNull(history.Latest);
        Assert.Null(history.Previous);
    }
}
