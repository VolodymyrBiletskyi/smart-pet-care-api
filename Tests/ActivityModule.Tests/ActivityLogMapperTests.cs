using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
using smart_pet_care_api.Modules.ActivityModule.Mapper;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class ActivityLogMapperTests
{
    [Fact]
    public void ToEntity_CarriesTheReadingAndStampsPetAndSource()
    {
        var petId = Guid.NewGuid();
        var recordedAt = DateTime.UtcNow.AddHours(-2);
        var reading = new ActivityReading(
            recordedAt, 3200, "Beach", "Chased a seagull",
            ActivityType.Run, ActivityIntensity.High, 45);

        var entity = ActivityLogMapper.ToEntity(reading, petId, ActivitySource.Device);

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(petId, entity.PetId);
        Assert.Equal(recordedAt, entity.RecordedAt);
        Assert.Equal(3200, entity.Steps);
        Assert.Equal("Beach", entity.Location);
        Assert.Equal("Chased a seagull", entity.Note);
        Assert.Equal(ActivityType.Run, entity.Type);
        Assert.Equal(ActivityIntensity.High, entity.Intensity);
        Assert.Equal(45, entity.DurationMinutes);
        Assert.Equal(ActivitySource.Device, entity.Source);
        Assert.InRange(entity.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void ToDto_CopiesEveryField()
    {
        var log = new ActivityLog
        {
            PetId = Guid.NewGuid(),
            RecordedAt = DateTime.UtcNow.AddDays(-1),
            Steps = 900,
            Location = "Yard",
            Note = "Short one",
            Type = ActivityType.Walk,
            Intensity = ActivityIntensity.Low,
            DurationMinutes = 50,
            Source = ActivitySource.Manual,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var dto = log.ToDto();

        Assert.Equal(log.Id, dto.Id);
        Assert.Equal(log.PetId, dto.PetId);
        Assert.Equal(log.RecordedAt, dto.RecordedAt);
        Assert.Equal(log.Steps, dto.Steps);
        Assert.Equal(log.Location, dto.Location);
        Assert.Equal(log.Note, dto.Note);
        Assert.Equal(log.Type, dto.Type);
        Assert.Equal(log.Intensity, dto.Intensity);
        Assert.Equal(log.DurationMinutes, dto.DurationMinutes);
        Assert.Equal(log.Source, dto.Source);
        Assert.Equal(log.CreatedAt, dto.CreatedAt);

        // 50 minutes at 0.4.
        Assert.Equal(20, dto.ActiveMinutes);
    }

    /// <summary>
    /// Rows written before durations existed still map; they simply contribute no active
    /// minutes rather than a zero that would drag a day's average down.
    /// </summary>
    [Fact]
    public void ToDto_LeavesActiveMinutesNullWhenTheLogCannotSupportIt()
    {
        var log = new ActivityLog { PetId = Guid.NewGuid(), Steps = 900 };

        Assert.Null(log.ToDto().ActiveMinutes);
        Assert.Null(new ActivityLog { DurationMinutes = 30 }.ToDto().ActiveMinutes);
        Assert.Null(new ActivityLog { Intensity = ActivityIntensity.High }.ToDto().ActiveMinutes);
    }

    [Fact]
    public void NormalizeToUtc_ConvertsLocalAndAssumesUtcForUnspecified()
    {
        var utc = DateTime.UtcNow;
        Assert.Equal(utc, ActivityLogMapper.NormalizeToUtc(utc));

        var local = DateTime.Now;
        Assert.Equal(local.ToUniversalTime(), ActivityLogMapper.NormalizeToUtc(local));

        var unspecified = DateTime.SpecifyKind(new DateTime(2026, 8, 20, 10, 0, 0), DateTimeKind.Unspecified);
        var normalized = ActivityLogMapper.NormalizeToUtc(unspecified);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(unspecified.Ticks, normalized.Ticks);
    }
}
