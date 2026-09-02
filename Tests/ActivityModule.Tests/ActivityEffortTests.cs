using smart_pet_care_api.Modules.ActivityModule.Domain;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class ActivityEffortTests
{
    [Theory]
    [InlineData(ActivityIntensity.Low, 60, 24)]
    [InlineData(ActivityIntensity.Moderate, 60, 42)]
    [InlineData(ActivityIntensity.High, 60, 60)]
    public void ActiveMinutes_WeighsDurationByIntensity(ActivityIntensity intensity, int duration, int expected)
    {
        Assert.Equal(expected, ActivityEffort.ActiveMinutes(duration, intensity));
    }

    [Fact]
    public void ActiveMinutes_RoundsHalvesAwayFromZero()
    {
        // 25 × 0.7 = 17.5.
        Assert.Equal(18, ActivityEffort.ActiveMinutes(25, ActivityIntensity.Moderate));
    }

    [Fact]
    public void ActiveMinutes_NeedsBothHalvesOfTheProduct()
    {
        Assert.Null(ActivityEffort.ActiveMinutes(null, ActivityIntensity.High));
        Assert.Null(ActivityEffort.ActiveMinutes(30, null));
        Assert.Null(ActivityEffort.ActiveMinutes(null, null));
    }

    /// <summary>
    /// Ordering matters more than the exact weights: whatever they are retuned to, a harder
    /// session must never count for less than an easier one of the same length.
    /// </summary>
    [Fact]
    public void WeightFor_IncreasesWithIntensity()
    {
        Assert.True(ActivityEffort.WeightFor(ActivityIntensity.Low) > 0);
        Assert.True(ActivityEffort.WeightFor(ActivityIntensity.Low) < ActivityEffort.WeightFor(ActivityIntensity.Moderate));
        Assert.True(ActivityEffort.WeightFor(ActivityIntensity.Moderate) < ActivityEffort.WeightFor(ActivityIntensity.High));
        Assert.True(ActivityEffort.WeightFor(ActivityIntensity.High) <= 1m);
    }

    /// <summary>
    /// Every type must resolve to something, including values appended to the enum later —
    /// a new activity that scored as nothing would be worse than one scored as average.
    /// </summary>
    [Fact]
    public void DefaultIntensityFor_CoversEveryTypeAndTheAbsenceOfOne()
    {
        foreach (var type in Enum.GetValues<ActivityType>())
            Assert.True(Enum.IsDefined(ActivityEffort.DefaultIntensityFor(type)));

        Assert.Equal(ActivityIntensity.Moderate, ActivityEffort.DefaultIntensityFor(null));
        Assert.Equal(ActivityIntensity.Moderate, ActivityEffort.DefaultIntensityFor((ActivityType)99));
    }
}
