using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.WellnessModule.Domain;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class WellnessDataAggregatorTests
{
    [Fact]
    public async Task AggregateAsync_UsesThirtyDayWindowAndOmitsUnavailableDimensions()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        var pet = new Pet
        {
            UserId = userId,
            Name = "Milo",
            Species = AnimalSpecies.Cat,
            BirthDate = new DateTime(2024, 8, 25),
            Sex = Sex.Unknown
        };
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = await new WellnessDataAggregator(db).AggregateAsync(
            pet.Id,
            userId,
            null,
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        Assert.Equal("cat", request.Pet.Species);
        Assert.Equal(23, request.Pet.AgeMonths);
        Assert.Null(request.Pet.Sex);
        Assert.Null(request.Activity);
        Assert.Null(request.Feeding);
        Assert.Null(request.PreventiveCare);
        Assert.Empty(request.ActiveConditions);
        Assert.Equal(new DateOnly(2026, 7, 26), request.EvaluationWindow!.StartDate);
        Assert.Equal(new DateOnly(2026, 8, 24), request.EvaluationWindow.EndDate);
    }

    [Fact]
    public async Task AggregateAsync_MapsTrackedDataAndPreviousScore()
    {
        await using var db = CreateContext();
        var userId = Guid.NewGuid();
        var pet = new Pet
        {
            UserId = userId,
            Name = "Luna",
            Species = AnimalSpecies.Dog,
            Sex = Sex.Female,
            WeightKg = 20
        };
        db.Pets.Add(pet);
        db.ActivityDailies.AddRange(
            new ActivityDaily { PetId = pet.Id, ActivityDate = Utc(2026, 8, 23), Steps = 8000, ActiveMinutes = 60, SleepHours = 10 },
            new ActivityDaily { PetId = pet.Id, ActivityDate = Utc(2026, 8, 24), Steps = 10000, ActiveMinutes = 80, SleepHours = 12 });
        db.FeedingLogs.AddRange(
            new FeedingLog { PetId = pet.Id, FedAt = Utc(2026, 8, 23), FoodType = FoodType.DryFood, ApproxCalories = 300 },
            new FeedingLog { PetId = pet.Id, FedAt = Utc(2026, 8, 24), FoodType = FoodType.WetFood, ApproxCalories = 300 });
        db.PetConditions.Add(new PetCondition
        {
            PetId = pet.Id,
            Name = "Arthritis",
            Type = ConditionType.Chronic,
            IsActive = true
        });
        db.PetWeightLogs.Add(new PetWeightLog
        {
            PetId = pet.Id,
            WeightKg = 20,
            MeasuredAt = Utc(2026, 8, 20)
        });
        db.PetEvents.Add(new PetEvent
        {
            PetId = pet.Id,
            Title = "Annual vaccination",
            Type = PetEventType.Vaccination,
            Status = PetEventStatus.Completed,
            ScheduledAt = Utc(2026, 8, 1)
        });
        db.Reminders.AddRange(
            new Reminder
            {
                PetId = pet.Id,
                Title = "Bathing",
                Type = ReminderType.Bathing,
                LastCompletedAt = Utc(2026, 8, 10)
            },
            new Reminder
            {
                PetId = pet.Id,
                Title = "Brushing",
                Type = ReminderType.Brushing
            });
        db.PetWellnessAssessments.Add(new PetWellnessAssessment
        {
            PetId = pet.Id,
            WellnessScore = 77,
            ScoreStatus = "Complete",
            DataCoverage = 1,
            CalculationVersion = "1.0.0",
            EvaluatedAt = Utc(2026, 8, 1),
            ResponseJson = "{}"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = await new WellnessDataAggregator(db).AggregateAsync(
            pet.Id,
            userId,
            "  low appetite  ",
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        Assert.Equal(9000, request.Activity!.AvgStepsPerDay);
        Assert.Equal(2, request.Activity.DaysTracked);
        Assert.Equal(0.07m, request.Feeding!.AvgMealsPerDay);
        Assert.Equal(20m, request.Feeding.AvgCaloriesPerDay);
        Assert.Equal(2, request.Feeding.ConsistencyDays);
        Assert.Equal("Arthritis", Assert.Single(request.ActiveConditions).Name);
        Assert.Equal(20, Assert.Single(request.WeightHistory).WeightKg);
        Assert.True(request.PreventiveCare!.VaccinationsUpToDate);
        Assert.Equal(2, request.RoutineCare.Count);
        Assert.Equal(
            new DateOnly(2026, 8, 10),
            request.RoutineCare.Single(item => item.Type == ClassifierWellnessReminderType.Bathing).LastDoneAt);
        Assert.Null(request.RoutineCare.Single(item => item.Type == ClassifierWellnessReminderType.Brushing).LastDoneAt);
        Assert.Equal(77, request.PreviousScore);
        Assert.Equal("low appetite", request.CurrentSymptoms);
    }

    [Fact]
    public async Task AggregateAsync_HidesPetsOwnedByAnotherUser()
    {
        await using var db = CreateContext();
        var pet = new Pet
        {
            UserId = Guid.NewGuid(),
            Name = "Private",
            Species = AnimalSpecies.Dog
        };
        db.Pets.Add(pet);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WellnessDataAggregator(db).AggregateAsync(
                pet.Id,
                Guid.NewGuid(),
                null,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
