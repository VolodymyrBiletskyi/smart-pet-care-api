using smart_pet_care_api.Common.Patching;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NutritionModule.Domain;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.Tests;

public class NutritionServiceTests
{
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private static NutritionService BuildService(
        FakeNutritionGoalRepository goalRepo,
        FakePetRepository petRepo,
        FakeFeedingLogRepository feedingRepo,
        FakeReminderRepository reminderRepo) =>
        new(goalRepo, petRepo, feedingRepo, reminderRepo);

    private FeedingLog Log(int? calories, decimal? amount, PortionUnit? unit) => new()
    {
        PetId = _petId,
        FedAt = DateTime.UtcNow,
        FoodType = FoodType.DryFood,
        ApproxCalories = calories,
        PortionAmount = amount,
        PortionUnit = unit
    };

    // ----- ownership / validation -----

    [Fact]
    public async Task GetDailySummary_WhenPetDoesNotBelongToUser_ThrowsBeforeQuerying()
    {
        var feeding = new FakeFeedingLogRepository();
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository { PetExists = false }, feeding, new FakeReminderRepository());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetDailySummaryAsync(_petId, _userId, null, 0));

        Assert.Equal("Pet not found", ex.Message);
        Assert.Null(feeding.RequestedPetId);
    }

    [Theory]
    [InlineData(-841)]
    [InlineData(841)]
    public async Task GetDailySummary_WhenOffsetOutOfRange_ThrowsArgumentException(int offset)
    {
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetDailySummaryAsync(_petId, _userId, null, offset));
    }

    // ----- day-window / offset math -----

    [Fact]
    public async Task GetDailySummary_ComputesLocalDayWindowInUtc()
    {
        var feeding = new FakeFeedingLogRepository();
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), feeding, new FakeReminderRepository());

        // 2026-07-15 local at UTC+2 -> [2026-07-14T22:00Z, 2026-07-15T22:00Z)
        var date = new DateOnly(2026, 7, 15);
        await service.GetDailySummaryAsync(_petId, _userId, date, 120);

        Assert.Equal(new DateTime(2026, 7, 14, 22, 0, 0, DateTimeKind.Utc), feeding.RequestedStartUtc);
        Assert.Equal(new DateTime(2026, 7, 15, 22, 0, 0, DateTimeKind.Utc), feeding.RequestedEndUtc);
        Assert.Equal(DateTimeKind.Utc, feeding.RequestedStartUtc!.Value.Kind);
    }

    [Fact]
    public async Task GetDailySummary_WhenNoDate_DefaultsToLocalToday()
    {
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        var summary = await service.GetDailySummaryAsync(_petId, _userId, null, 600);

        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(600)), summary.Date);
    }

    // ----- aggregation -----

    [Fact]
    public async Task GetDailySummary_AggregatesCaloriesMealsAndPortionsByUnit()
    {
        var feeding = new FakeFeedingLogRepository
        {
            Logs =
            [
                Log(100, 100m, PortionUnit.Gram),
                Log(150, 150m, PortionUnit.Gram),
                Log(null, 50m, PortionUnit.Milliliter),
                Log(30, null, null)
            ]
        };
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), feeding, new FakeReminderRepository());

        var summary = await service.GetDailySummaryAsync(_petId, _userId, new DateOnly(2026, 7, 15), 0);

        Assert.Equal(4, summary.MealCount);
        Assert.Equal(280, summary.TotalCalories); // 100 + 150 + 0 + 30
        Assert.Equal(2, summary.PortionTotalsByUnit.Count);
        Assert.Equal(250m, summary.PortionTotalsByUnit.Single(p => p.Unit == PortionUnit.Gram).TotalAmount);
        Assert.Equal(50m, summary.PortionTotalsByUnit.Single(p => p.Unit == PortionUnit.Milliliter).TotalAmount);
        Assert.Null(summary.Comparison); // no goal
    }

    [Fact]
    public async Task GetDailySummary_CountsOnlyActiveFeedingReminders()
    {
        var reminders = new FakeReminderRepository
        {
            Reminders =
            [
                new Reminder { Type = ReminderType.Feeding, Status = ReminderStatus.Active },
                new Reminder { Type = ReminderType.Feeding, Status = ReminderStatus.Completed },
                new Reminder { Type = ReminderType.Medication, Status = ReminderStatus.Active },
                new Reminder { Type = ReminderType.Feeding, Status = ReminderStatus.Active }
            ]
        };
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), new FakeFeedingLogRepository(), reminders);

        var summary = await service.GetDailySummaryAsync(_petId, _userId, new DateOnly(2026, 7, 15), 0);

        Assert.Equal(2, summary.ScheduledFeedings);
    }

    // ----- goal comparison -----

    [Fact]
    public async Task GetDailySummary_ComparesAgainstGoal()
    {
        var goal = new NutritionGoal
        {
            PetId = _petId,
            DailyCalorieTarget = 300,
            DailyPortionTarget = 300m,
            PortionUnit = PortionUnit.Gram,
            MealsPerDay = 3
        };
        var feeding = new FakeFeedingLogRepository
        {
            Logs = [Log(100, 100m, PortionUnit.Gram), Log(150, 150m, PortionUnit.Gram)]
        };
        var service = BuildService(
            new FakeNutritionGoalRepository { Goal = goal }, new FakePetRepository(), feeding, new FakeReminderRepository());

        var summary = await service.GetDailySummaryAsync(_petId, _userId, new DateOnly(2026, 7, 15), 0);

        Assert.NotNull(summary.Comparison);
        var c = summary.Comparison!;
        Assert.Equal(50, c.CaloriesRemaining);   // 300 - 250
        Assert.False(c.CalorieTargetMet);
        Assert.Equal(1, c.MealsRemaining);        // 3 - 2
        Assert.False(c.MealsTargetMet);
        Assert.Equal(50m, c.PortionRemaining);    // 300 - 250 (Gram)
        Assert.False(c.PortionTargetMet);
    }

    [Fact]
    public async Task GetDailySummary_WhenGoalHasNoPortionTarget_PortionComparisonIsNull()
    {
        var goal = new NutritionGoal { PetId = _petId, DailyCalorieTarget = 100 };
        var feeding = new FakeFeedingLogRepository { Logs = [Log(120, 100m, PortionUnit.Gram)] };
        var service = BuildService(
            new FakeNutritionGoalRepository { Goal = goal }, new FakePetRepository(), feeding, new FakeReminderRepository());

        var summary = await service.GetDailySummaryAsync(_petId, _userId, new DateOnly(2026, 7, 15), 0);

        Assert.NotNull(summary.Comparison);
        var c = summary.Comparison!;
        Assert.True(c.CalorieTargetMet);        // 120 >= 100
        Assert.Null(c.PortionRemaining);
        Assert.Null(c.PortionTargetMet);
        Assert.Null(c.MealsRemaining);
    }

    [Fact]
    public async Task GetDailySummary_PortionComparisonIgnoresLogsInOtherUnits()
    {
        var goal = new NutritionGoal
        {
            PetId = _petId,
            DailyPortionTarget = 200m,
            PortionUnit = PortionUnit.Gram
        };
        // Logged only in Milliliter -> nothing counts toward the Gram target.
        var feeding = new FakeFeedingLogRepository { Logs = [Log(null, 500m, PortionUnit.Milliliter)] };
        var service = BuildService(
            new FakeNutritionGoalRepository { Goal = goal }, new FakePetRepository(), feeding, new FakeReminderRepository());

        var summary = await service.GetDailySummaryAsync(_petId, _userId, new DateOnly(2026, 7, 15), 0);

        Assert.NotNull(summary.Comparison);
        var c = summary.Comparison!;
        Assert.Equal(200m, c.PortionRemaining);
        Assert.False(c.PortionTargetMet);
    }

    // ----- goal CRUD -----

    [Fact]
    public async Task UpsertGoal_WhenNoneExists_AddsGoal()
    {
        var goalRepo = new FakeNutritionGoalRepository();
        var service = BuildService(
            goalRepo, new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        var dto = new UpsertNutritionGoalDto
        {
            DailyCalorieTarget = 400,
            DailyPortionTarget = 250m,
            PortionUnit = PortionUnit.Gram,
            MealsPerDay = 2
        };
        var result = await service.UpsertGoalAsync(_petId, _userId, dto);

        Assert.NotNull(goalRepo.AddedGoal);
        Assert.Equal(_petId, result.PetId);
        Assert.Equal(400, result.DailyCalorieTarget);
        Assert.Equal(1, goalRepo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpsertGoal_WhenExists_ReplacesWithoutAdding()
    {
        var goalRepo = new FakeNutritionGoalRepository
        {
            Goal = new NutritionGoal { PetId = _petId, DailyCalorieTarget = 100, MealsPerDay = 1 }
        };
        var service = BuildService(
            goalRepo, new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        var result = await service.UpsertGoalAsync(_petId, _userId, new UpsertNutritionGoalDto { DailyCalorieTarget = 500 });

        Assert.Null(goalRepo.AddedGoal);
        Assert.Equal(500, result.DailyCalorieTarget);
        Assert.Null(result.MealsPerDay); // replaced, not merged
    }

    [Fact]
    public async Task UpsertGoal_WhenPortionTargetHasNoUnit_ThrowsArgumentException()
    {
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertGoalAsync(_petId, _userId, new UpsertNutritionGoalDto { DailyPortionTarget = 100m }));
    }

    [Fact]
    public async Task UpsertGoal_WhenNegativeCalories_ThrowsArgumentException()
    {
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertGoalAsync(_petId, _userId, new UpsertNutritionGoalDto { DailyCalorieTarget = -1 }));
    }

    [Fact]
    public async Task PatchGoal_WhenNoneExists_ThrowsNotFound()
    {
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PatchGoalAsync(_petId, _userId, new PatchNutritionGoalDto()));

        Assert.Equal("Nutrition goal not found", ex.Message);
    }

    [Fact]
    public async Task PatchGoal_UpdatesOnlyProvidedFields()
    {
        var goalRepo = new FakeNutritionGoalRepository
        {
            Goal = new NutritionGoal { PetId = _petId, DailyCalorieTarget = 100, MealsPerDay = 2 }
        };
        var service = BuildService(
            goalRepo, new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        var dto = new PatchNutritionGoalDto { DailyCalorieTarget = PatchField<int?>.Set(250) };
        var result = await service.PatchGoalAsync(_petId, _userId, dto);

        Assert.Equal(250, result.DailyCalorieTarget);
        Assert.Equal(2, result.MealsPerDay); // untouched
    }

    [Fact]
    public async Task DeleteGoal_WhenNoneExists_ReturnsFalse()
    {
        var service = BuildService(
            new FakeNutritionGoalRepository(), new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        Assert.False(await service.DeleteGoalAsync(_petId, _userId));
    }

    [Fact]
    public async Task DeleteGoal_WhenExists_DeletesAndReturnsTrue()
    {
        var goalRepo = new FakeNutritionGoalRepository { Goal = new NutritionGoal { PetId = _petId } };
        var service = BuildService(
            goalRepo, new FakePetRepository(), new FakeFeedingLogRepository(), new FakeReminderRepository());

        Assert.True(await service.DeleteGoalAsync(_petId, _userId));
        Assert.NotNull(goalRepo.DeletedGoal);
    }
}
