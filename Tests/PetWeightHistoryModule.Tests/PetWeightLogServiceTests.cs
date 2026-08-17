using smart_pet_care_api.Common.Patching;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Domain;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using Xunit;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Tests;

public class PetWeightLogServiceTests
{
    private readonly Guid _petId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task GetByPetIdAsync_WhenPetDoesNotBelongToUser_ThrowsNotFoundBeforeQueryingLogs()
    {
        var repo = new FakePetWeightLogRepository { PetBelongsToUser = false };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetByPetIdAsync(_petId, _userId));

        Assert.Equal("Pet not found", exception.Message);
        Assert.Equal(0, repo.GetByPetIdCalls);
    }

    [Fact]
    public async Task GetByPetIdAsync_MapsLogsAndPassesNullPeriod()
    {
        var log = NewLog();
        var repo = new FakePetWeightLogRepository { Logs = [log] };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var result = await service.GetByPetIdAsync(_petId, _userId);

        var dto = Assert.Single(result);
        Assert.Equal(log.Id, dto.Id);
        Assert.Null(repo.RequestedFrom);
        Assert.Null(repo.RequestedTo);
    }

    [Fact]
    public async Task GetByPetIdAsync_NormalizesUnspecifiedFromAsUtc()
    {
        var from = new DateTime(2026, 7, 1, 12, 30, 0, DateTimeKind.Unspecified);
        var repo = new FakePetWeightLogRepository();
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        await service.GetByPetIdAsync(_petId, _userId, from);

        Assert.Equal(DateTimeKind.Utc, repo.RequestedFrom!.Value.Kind);
        Assert.Equal(from, repo.RequestedFrom.Value);
    }

    [Fact]
    public async Task GetByPetIdAsync_NormalizesLocalFromToUtc()
    {
        var from = new DateTime(2026, 7, 1, 12, 30, 0, DateTimeKind.Local);
        var repo = new FakePetWeightLogRepository();
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        await service.GetByPetIdAsync(_petId, _userId, from);

        Assert.Equal(from.ToUniversalTime(), repo.RequestedFrom);
    }

    [Fact]
    public async Task GetByPetIdAsync_DateOnlyToIncludesEntireDay()
    {
        var to = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var repo = new FakePetWeightLogRepository();
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        await service.GetByPetIdAsync(_petId, _userId, to: to);

        Assert.Equal(to.Date.AddDays(1).AddTicks(-1), repo.RequestedTo);
    }

    [Fact]
    public async Task GetByPetIdAsync_ToWithTimePreservesExactTime()
    {
        var to = new DateTime(2026, 7, 1, 16, 45, 0, DateTimeKind.Utc);
        var repo = new FakePetWeightLogRepository();
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        await service.GetByPetIdAsync(_petId, _userId, to: to);

        Assert.Equal(to, repo.RequestedTo);
    }

    [Fact]
    public async Task GetByPetIdAsync_WhenFromIsAfterTo_ThrowsArgumentException()
    {
        var service = new PetWeightLogService(new FakePetWeightLogRepository(), new FakeReminderRecalculationService());
        var from = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetByPetIdAsync(_petId, _userId, from, to));

        Assert.Equal("From cannot be later than To", exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("230.01")]
    public async Task CreateAsync_WhenWeightIsOutsideRange_ThrowsArgumentException(string rawWeight)
    {
        var service = new PetWeightLogService(new FakePetWeightLogRepository(), new FakeReminderRecalculationService());
        var dto = ValidCreate(decimal.Parse(rawWeight, System.Globalization.CultureInfo.InvariantCulture));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(_petId, _userId, dto));

        Assert.Contains("WeightKg", exception.Message);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("230")]
    public async Task CreateAsync_AcceptsWeightRangeBoundaries(string rawWeight)
    {
        var weight = decimal.Parse(rawWeight, System.Globalization.CultureInfo.InvariantCulture);
        var repo = new FakePetWeightLogRepository { TrackedPet = new Pet { Id = _petId } };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var result = await service.CreateAsync(_petId, _userId, ValidCreate(weight));

        Assert.Equal(weight, result.WeightKg);
    }

    [Fact]
    public async Task CreateAsync_WhenMeasuredAtIsNull_ThrowsRequiredMessage()
    {
        var service = new PetWeightLogService(new FakePetWeightLogRepository(), new FakeReminderRecalculationService());
        var dto = ValidCreate();
        dto.MeasuredAt = null;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(_petId, _userId, dto));

        Assert.Equal("MeasuredAt is required", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenMeasuredAtIsDefault_ThrowsRequiredMessage()
    {
        var service = new PetWeightLogService(new FakePetWeightLogRepository(), new FakeReminderRecalculationService());
        var dto = ValidCreate();
        dto.MeasuredAt = default(DateTime);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(_petId, _userId, dto));

        Assert.Equal("MeasuredAt is required", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenMeasuredAtIsMoreThanTenMinutesFuture_ThrowsArgumentException()
    {
        var service = new PetWeightLogService(new FakePetWeightLogRepository(), new FakeReminderRecalculationService());
        var dto = ValidCreate();
        dto.MeasuredAt = DateTime.UtcNow.AddMinutes(11);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(_petId, _userId, dto));

        Assert.Equal("MeasuredAt cannot be more than 10 minutes in the future", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public async Task CreateAsync_WhenNotesAreWhitespace_ThrowsArgumentException(string notes)
    {
        var service = new PetWeightLogService(new FakePetWeightLogRepository(), new FakeReminderRecalculationService());
        var dto = ValidCreate();
        dto.Notes = notes;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(_petId, _userId, dto));

        Assert.Equal("Notes cannot be whitespace only", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenTimestampAlreadyExists_ThrowsConflictWithoutSaving()
    {
        var repo = new FakePetWeightLogRepository { MeasuredAtExists = true };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var exception = await Assert.ThrowsAsync<PetWeightLogConflictException>(() =>
            service.CreateAsync(_petId, _userId, ValidCreate()));

        Assert.Contains("already exists", exception.Message);
        Assert.Null(repo.AddedLog);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_StoresUtcLogRefreshesPetWeightAndSavesTwice()
    {
        var measuredAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var pet = new Pet { Id = _petId, WeightKg = 5m };
        var repo = new FakePetWeightLogRepository { TrackedPet = pet };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());
        var before = DateTime.UtcNow;
        var dto = ValidCreate(12.3m);
        dto.MeasuredAt = measuredAt;
        dto.Notes = null;

        var result = await service.CreateAsync(_petId, _userId, dto);

        Assert.NotNull(repo.AddedLog);
        Assert.Equal(DateTimeKind.Utc, repo.AddedLog.MeasuredAt.Kind);
        Assert.Equal(measuredAt, repo.AddedLog.MeasuredAt);
        Assert.Equal(12.3m, pet.WeightKg);
        Assert.InRange(pet.UpdatedAt!.Value, before, DateTime.UtcNow);
        Assert.Equal(2, repo.SaveChangesCalls);
        Assert.Equal(repo.AddedLog.Id, result.Id);
        Assert.Equal(repo.AddedLog.MeasuredAt, repo.CheckedMeasuredAt);
        Assert.Null(repo.CheckedExcludeId);
    }

    [Fact]
    public async Task CreateAsync_WhenTrackedPetDisappears_ThrowsAfterInitialSave()
    {
        var repo = new FakePetWeightLogRepository { TrackedPet = null };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(_petId, _userId, ValidCreate()));

        Assert.Equal("Pet not found", exception.Message);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_WhenPetDoesNotBelongToUser_ThrowsBeforeLoadingLog()
    {
        var repo = new FakePetWeightLogRepository { PetBelongsToUser = false, TrackedLog = NewLog() };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(_petId, Guid.NewGuid(), _userId, new PatchPetWeightLogDto()));

        Assert.Equal("Pet not found", exception.Message);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_WhenLogIsMissing_ThrowsNotFound()
    {
        var service = new PetWeightLogService(new FakePetWeightLogRepository { TrackedLog = null }, new FakeReminderRecalculationService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(_petId, Guid.NewGuid(), _userId, new PatchPetWeightLogDto()));

        Assert.Equal("Weight log not found", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenLogBelongsToAnotherPet_ThrowsNotFound()
    {
        var repo = new FakePetWeightLogRepository { TrackedLog = NewLog(Guid.NewGuid()) };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(_petId, repo.TrackedLog.Id, _userId, new PatchPetWeightLogDto()));

        Assert.Equal("Weight log not found", exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("230.01")]
    public async Task UpdateAsync_WhenPatchedWeightIsInvalid_ThrowsArgumentException(string rawWeight)
    {
        var repo = new FakePetWeightLogRepository { TrackedLog = NewLog() };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());
        var dto = new PatchPetWeightLogDto
        {
            WeightKg = PatchField<decimal>.Set(decimal.Parse(rawWeight, System.Globalization.CultureInfo.InvariantCulture))
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(_petId, repo.TrackedLog.Id, _userId, dto));
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_WhenPatchedMeasuredAtIsDefault_ThrowsRequiredMessage()
    {
        var repo = new FakePetWeightLogRepository { TrackedLog = NewLog() };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());
        var dto = new PatchPetWeightLogDto { MeasuredAt = PatchField<DateTime>.Set(default) };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(_petId, repo.TrackedLog.Id, _userId, dto));

        Assert.Equal("MeasuredAt is required", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenPatchedNotesAreWhitespace_ThrowsArgumentException()
    {
        var repo = new FakePetWeightLogRepository { TrackedLog = NewLog() };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());
        var dto = new PatchPetWeightLogDto { Notes = PatchField<string?>.Set("  ") };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(_petId, repo.TrackedLog.Id, _userId, dto));

        Assert.Equal("Notes cannot be whitespace only", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_ValidatesUnchangedFinalState()
    {
        var invalidLog = NewLog();
        invalidLog.WeightKg = 0;
        var repo = new FakePetWeightLogRepository { TrackedLog = invalidLog };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(_petId, invalidLog.Id, _userId, new PatchPetWeightLogDto()));

        Assert.Equal("WeightKg must be greater than 0", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenTimestampConflicts_ExcludesCurrentIdAndDoesNotSave()
    {
        var log = NewLog();
        var repo = new FakePetWeightLogRepository { TrackedLog = log, MeasuredAtExists = true };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        await Assert.ThrowsAsync<PetWeightLogConflictException>(() =>
            service.UpdateAsync(_petId, log.Id, _userId, new PatchPetWeightLogDto()));

        Assert.Equal(log.Id, repo.CheckedExcludeId);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_PatchesFieldsClearsNotesRefreshesWeightAndSavesTwice()
    {
        var log = NewLog();
        var latest = NewLog();
        latest.WeightKg = 25m;
        latest.MeasuredAt = DateTime.UtcNow;
        var pet = new Pet { Id = _petId, WeightKg = 10m };
        var repo = new FakePetWeightLogRepository { TrackedLog = log, LatestLog = latest, TrackedPet = pet };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());
        var measuredAt = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Unspecified);
        var before = DateTime.UtcNow;
        var dto = new PatchPetWeightLogDto
        {
            WeightKg = PatchField<decimal>.Set(20m),
            MeasuredAt = PatchField<DateTime>.Set(measuredAt),
            Notes = PatchField<string?>.Set(null)
        };

        var result = await service.UpdateAsync(_petId, log.Id, _userId, dto);

        Assert.Equal(20m, log.WeightKg);
        Assert.Equal(DateTimeKind.Utc, log.MeasuredAt.Kind);
        Assert.Null(log.Notes);
        Assert.InRange(log.UpdatedAt!.Value, before, DateTime.UtcNow);
        Assert.Equal(25m, pet.WeightKg);
        Assert.Equal(2, repo.SaveChangesCalls);
        Assert.Equal(log.Id, result.Id);
    }

    [Fact]
    public async Task DeleteAsync_WhenPetDoesNotBelongToUser_ThrowsNotFound()
    {
        var repo = new FakePetWeightLogRepository { PetBelongsToUser = false };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(_petId, Guid.NewGuid(), _userId));

        Assert.Equal("Pet not found", exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_WhenLogIsMissing_ReturnsFalseWithoutSaving()
    {
        var repo = new FakePetWeightLogRepository { TrackedLog = null };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var result = await service.DeleteAsync(_petId, Guid.NewGuid(), _userId);

        Assert.False(result);
        Assert.Null(repo.DeletedLog);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_WhenLogBelongsToAnotherPet_ReturnsFalseWithoutSaving()
    {
        var repo = new FakePetWeightLogRepository { TrackedLog = NewLog(Guid.NewGuid()) };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var result = await service.DeleteAsync(_petId, repo.TrackedLog.Id, _userId);

        Assert.False(result);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_DeletesLogRefreshesLatestWeightAndSavesTwice()
    {
        var log = NewLog();
        var latest = NewLog();
        latest.WeightKg = 8.5m;
        var pet = new Pet { Id = _petId, WeightKg = log.WeightKg };
        var repo = new FakePetWeightLogRepository { TrackedLog = log, LatestLog = latest, TrackedPet = pet };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        var result = await service.DeleteAsync(_petId, log.Id, _userId);

        Assert.True(result);
        Assert.Same(log, repo.DeletedLog);
        Assert.Equal(8.5m, pet.WeightKg);
        Assert.Equal(2, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_WhenNoLogsRemain_ClearsPetCurrentWeight()
    {
        var log = NewLog();
        var pet = new Pet { Id = _petId, WeightKg = log.WeightKg };
        var repo = new FakePetWeightLogRepository { TrackedLog = log, LatestLog = null, TrackedPet = pet };
        var service = new PetWeightLogService(repo, new FakeReminderRecalculationService());

        await service.DeleteAsync(_petId, log.Id, _userId);

        Assert.Null(pet.WeightKg);
    }

    private CreatePetWeightLogDto ValidCreate(decimal weight = 10m) => new()
    {
        WeightKg = weight,
        MeasuredAt = DateTime.UtcNow.AddMinutes(-1),
        Notes = "Routine measurement"
    };

    private PetWeightLog NewLog(Guid? petId = null) => new()
    {
        Id = Guid.NewGuid(),
        PetId = petId ?? _petId,
        WeightKg = 10m,
        MeasuredAt = DateTime.UtcNow.AddDays(-1),
        Notes = "Initial",
        CreatedAt = DateTime.UtcNow.AddDays(-1)
    };

    [Fact]
    public async Task CreateAsync_WithReminderId_ClosesTheWeighingReminderFromTheMeasurementDate()
    {
        // Weighing carries a number the generic complete payload cannot hold, so creating the
        // measurement is what closes the reminder.
        var repo = new FakePetWeightLogRepository();
        var recalculation = new FakeReminderRecalculationService();
        var service = new PetWeightLogService(repo, recalculation);
        var reminderId = Guid.NewGuid();
        var measuredAt = DateTime.UtcNow.AddHours(-3);

        await service.CreateAsync(_petId, _userId, new CreatePetWeightLogDto
        {
            ReminderId = reminderId,
            WeightKg = 12.4m,
            MeasuredAt = measuredAt
        });

        Assert.Equal(1, recalculation.Calls);
        Assert.Equal(reminderId, recalculation.RegisteredReminderId);
        Assert.Equal(measuredAt, recalculation.RegisteredPerformedAt);
        Assert.Equal(_petId, recalculation.RegisteredPetId);
        Assert.Equal(reminderId, repo.AddedLog!.ReminderId);
    }

    [Fact]
    public async Task CreateAsync_WithoutReminderId_TouchesNoReminder()
    {
        var recalculation = new FakeReminderRecalculationService();
        var service = new PetWeightLogService(new FakePetWeightLogRepository(), recalculation);

        await service.CreateAsync(_petId, _userId, new CreatePetWeightLogDto
        {
            WeightKg = 12.4m,
            MeasuredAt = DateTime.UtcNow.AddHours(-3)
        });

        Assert.Equal(0, recalculation.Calls);
    }

    [Fact]
    public async Task CreateAsync_WithAReminderOnAnotherPet_IsRejected()
    {
        var recalculation = new FakeReminderRecalculationService { ReminderResolves = false };
        var service = new PetWeightLogService(new FakePetWeightLogRepository(), recalculation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            _petId, _userId, new CreatePetWeightLogDto
            {
                ReminderId = Guid.NewGuid(),
                WeightKg = 12.4m,
                MeasuredAt = DateTime.UtcNow.AddHours(-3)
            }));
    }
}
