using smart_pet_care_api.Common.Patching;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests;
using smart_pet_care_api.Modules.PetWeightHistoryModule.Mapper;
using Xunit;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Tests;

public class PetWeightLogMapperTests
{
    [Fact]
    public void NormalizeToUtc_UtcValueIsReturnedUnchanged()
    {
        var value = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(value, PetWeightLogMapper.NormalizeToUtc(value));
    }

    [Fact]
    public void NormalizeToUtc_LocalValueIsConvertedToUtc()
    {
        var value = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Local);
        Assert.Equal(value.ToUniversalTime(), PetWeightLogMapper.NormalizeToUtc(value));
    }

    [Fact]
    public void NormalizeToUtc_UnspecifiedValueKeepsClockTimeAndBecomesUtc()
    {
        var value = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var result = PetWeightLogMapper.NormalizeToUtc(value);
        Assert.Equal(value, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ToEntity_MapsEveryFieldAndGeneratesMetadata()
    {
        var petId = Guid.NewGuid();
        var measuredAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var before = DateTime.UtcNow;
        var dto = new CreatePetWeightLogDto { WeightKg = 4.5m, MeasuredAt = measuredAt, Notes = "note" };

        var result = PetWeightLogMapper.ToEntity(dto, petId);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(petId, result.PetId);
        Assert.Equal(4.5m, result.WeightKg);
        Assert.Equal(DateTimeKind.Utc, result.MeasuredAt.Kind);
        Assert.Equal("note", result.Notes);
        Assert.InRange(result.CreatedAt, before, DateTime.UtcNow);
        Assert.Null(result.UpdatedAt);
    }

    [Fact]
    public void ToDto_MapsEveryField()
    {
        var log = new PetWeightLog
        {
            Id = Guid.NewGuid(), PetId = Guid.NewGuid(), WeightKg = 8m,
            MeasuredAt = DateTime.UtcNow.AddDays(-2), Notes = "note",
            CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow
        };

        var result = log.ToDto();

        Assert.Equal(log.Id, result.Id);
        Assert.Equal(log.PetId, result.PetId);
        Assert.Equal(log.WeightKg, result.WeightKg);
        Assert.Equal(log.MeasuredAt, result.MeasuredAt);
        Assert.Equal(log.Notes, result.Notes);
        Assert.Equal(log.CreatedAt, result.CreatedAt);
        Assert.Equal(log.UpdatedAt, result.UpdatedAt);
    }

    [Fact]
    public void PatchEntity_UnsetFieldsRemainUnchangedAndUpdatedAtIsNotSet()
    {
        var log = NewLog();
        log.PatchEntity(new PatchPetWeightLogDto());

        Assert.Equal(5m, log.WeightKg);
        Assert.Equal("old", log.Notes);
        Assert.Null(log.UpdatedAt);
    }

    [Fact]
    public void PatchEntity_SetFieldsAreAppliedAndNotesCanBeCleared()
    {
        var log = NewLog();
        var measuredAt = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var dto = new PatchPetWeightLogDto
        {
            WeightKg = PatchField<decimal>.Set(7m),
            MeasuredAt = PatchField<DateTime>.Set(measuredAt),
            Notes = PatchField<string?>.Set(null)
        };

        log.PatchEntity(dto);

        Assert.Equal(7m, log.WeightKg);
        Assert.Equal(DateTimeKind.Utc, log.MeasuredAt.Kind);
        Assert.Null(log.Notes);
    }

    private static PetWeightLog NewLog() => new()
    {
        PetId = Guid.NewGuid(), WeightKg = 5m, MeasuredAt = DateTime.UtcNow.AddDays(-1), Notes = "old"
    };
}
