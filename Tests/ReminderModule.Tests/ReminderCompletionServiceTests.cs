using Xunit;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ReminderModule.Domain;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Tests;

public class ReminderCompletionServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed record Harness(
        ReminderCompletionService Service,
        FakeReminderRepository Reminders,
        FakeHealthRecordRepository HealthRecords,
        FakePetRepository Pets,
        Reminder Reminder);

    private static Harness Build(ReminderType type, RecalcStrategy strategy = RecalcStrategy.FromCompletion)
    {
        var anchor = DateTime.UtcNow.AddDays(-1);

        var reminder = new Reminder
        {
            PetId = Guid.NewGuid(),
            Title = "Rabies booster",
            Type = type,
            RepeatType = RepeatType.Monthly,
            IntervalN = 12,
            RecalcStrategy = strategy,
            Date = DateOnly.FromDateTime(anchor),
            TimeOfDay = new TimeSpan(5, 0, 0),
            UtcOffsetMinutes = 180,
            StartAt = anchor,
            ScheduleAnchorAt = anchor,
            NextTriggerAt = anchor,
            Status = ReminderStatus.Active
        };

        var reminderRepo = new FakeReminderRepository();
        reminderRepo.Reminders.Add(reminder);

        var healthRepo = new FakeHealthRecordRepository();
        var petRepo = new FakePetRepository();

        var service = new ReminderCompletionService(
            reminderRepo, new ReminderRecalculationService(reminderRepo), healthRepo, petRepo);

        return new Harness(service, reminderRepo, healthRepo, petRepo, reminder);
    }

    [Fact]
    public async Task Medical_completion_files_a_health_record_linked_to_the_reminder()
    {
        var harness = Build(ReminderType.Vaccination);
        var performedAt = DateTime.UtcNow.AddHours(-2);

        var result = await harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId, new CompleteReminderDto { PerformedAt = performedAt });

        var record = Assert.Single(harness.HealthRecords.Records);
        Assert.Equal(harness.Reminder.Id, record.ReminderId);
        Assert.Equal(HealthRecordType.Vaccination, record.Type);
        Assert.Equal(performedAt, record.PerformedAt);
        Assert.Equal(record.Id, result.HealthRecordId);
    }

    [Fact]
    public async Task Filed_record_takes_its_next_due_date_from_the_recalculated_trigger()
    {
        // Two sources of truth for the same date is exactly what this avoids.
        var harness = Build(ReminderType.Vaccination);

        await harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId, new CompleteReminderDto());

        var record = Assert.Single(harness.HealthRecords.Records);
        Assert.Equal(harness.Reminder.NextTriggerAt, record.NextDueAt);
    }

    [Fact]
    public async Task Parasite_treatment_maps_onto_the_differently_named_health_record_type()
    {
        var harness = Build(ReminderType.ParasiteTreatment);

        await harness.Service.CompleteAsync(harness.Reminder.Id, UserId, new CompleteReminderDto());

        var record = Assert.Single(harness.HealthRecords.Records);
        Assert.Equal(HealthRecordType.AntiParasiteTreatment, record.Type);
    }

    [Fact]
    public async Task Grooming_completion_files_no_health_record()
    {
        // Grooming history is the closed run itself; there is no record entity for it.
        var harness = Build(ReminderType.NailTrimming, RecalcStrategy.Calendar);

        var result = await harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId, new CompleteReminderDto { Note = "back left claw split" });

        Assert.Empty(harness.HealthRecords.Records);
        Assert.Null(result.HealthRecordId);
        Assert.Equal("back left claw split", result.Run.Note);
    }

    [Fact]
    public async Task Completion_defaults_to_now_when_no_date_is_given()
    {
        var harness = Build(ReminderType.Bathing, RecalcStrategy.Calendar);

        var before = DateTime.UtcNow;
        var result = await harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId, new CompleteReminderDto());

        Assert.NotNull(result.Run.PerformedAt);
        Assert.InRange(result.Run.PerformedAt!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task A_future_performed_date_is_rejected()
    {
        var harness = Build(ReminderType.Vaccination);

        await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId,
            new CompleteReminderDto { PerformedAt = DateTime.UtcNow.AddDays(1) }));
    }

    [Fact]
    public async Task A_reminder_on_someone_elses_pet_is_not_found()
    {
        var harness = Build(ReminderType.Vaccination);
        harness.Pets.Exists = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId, new CompleteReminderDto()));
    }

    [Fact]
    public async Task A_repeated_completion_does_not_file_a_second_record()
    {
        var harness = Build(ReminderType.Vaccination);
        var performedAt = DateTime.UtcNow.AddHours(-2);
        var dto = new CompleteReminderDto { PerformedAt = performedAt };

        await harness.Service.CompleteAsync(harness.Reminder.Id, UserId, dto);
        var second = await harness.Service.CompleteAsync(harness.Reminder.Id, UserId, dto);

        Assert.Single(harness.HealthRecords.Records);
        Assert.True(second.AlreadyRecorded);
        Assert.Equal(harness.HealthRecords.Records[0].Id, second.HealthRecordId);
    }

    [Fact]
    public async Task Weighing_cannot_be_completed_through_the_generic_endpoint()
    {
        // Closing it here would leave the user believing the weight was saved. The weight log
        // owns this completion.
        var harness = Build(ReminderType.Weighing, RecalcStrategy.Calendar);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId, new CompleteReminderDto()));

        Assert.Contains("weight-logs", error.Message);
        Assert.Empty(harness.Reminders.Runs);
    }

    [Fact]
    public async Task Feeding_cannot_be_completed_through_the_generic_endpoint()
    {
        var harness = Build(ReminderType.Feeding, RecalcStrategy.Calendar);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId, new CompleteReminderDto()));

        Assert.Contains("feeding-logs", error.Message);
    }

    [Fact]
    public async Task Dosage_on_a_type_that_files_no_health_record_is_rejected()
    {
        // Accepting it would drop it silently.
        var harness = Build(ReminderType.Bathing, RecalcStrategy.Calendar);

        await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.CompleteAsync(
            harness.Reminder.Id, UserId, new CompleteReminderDto { Dosage = "10 mg" }));
    }
}
