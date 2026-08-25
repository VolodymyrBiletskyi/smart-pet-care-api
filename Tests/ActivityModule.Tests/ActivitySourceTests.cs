using smart_pet_care_api.Modules.ActivityModule.Domain.Sources;
using smart_pet_care_api.Modules.ActivityModule.DTOs.Requests;
using Xunit;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Tests;

public class ActivitySourceTests
{
    [Fact]
    public async Task ManualProvider_NormalizesTheRequestBody()
    {
        var provider = new ManualActivitySourceProvider();
        var recordedAt = DateTime.SpecifyKind(new DateTime(2026, 8, 20, 7, 30, 0), DateTimeKind.Unspecified);

        var reading = await provider.ReadAsync(Guid.NewGuid(), new CreateActivityLogDto
        {
            RecordedAt = recordedAt,
            Steps = 2500,
            Location = "  River trail  ",
            Note = "   "
        });

        Assert.Equal(ActivitySource.Manual, provider.Source);
        Assert.Equal(DateTimeKind.Utc, reading.RecordedAt.Kind);
        Assert.Equal(recordedAt.Ticks, reading.RecordedAt.Ticks);
        Assert.Equal(2500, reading.Steps);
        Assert.Equal("River trail", reading.Location);
        Assert.Null(reading.Note);
    }

    [Fact]
    public void Resolver_ReturnsTheProviderRegisteredForASourceAndNullOtherwise()
    {
        var manual = new ManualActivitySourceProvider();
        var resolver = new ActivitySourceResolver([manual]);

        Assert.Same(manual, resolver.Resolve(ActivitySource.Manual));
        Assert.Null(resolver.Resolve(ActivitySource.Device));
        Assert.Null(resolver.Resolve(ActivitySource.Mock));
    }

    [Fact]
    public void Resolver_LetsALaterRegistrationReplaceAnEarlierOneForTheSameSource()
    {
        var replacement = new StubActivitySourceProvider(
            ActivitySource.Manual,
            new ActivityReading(DateTime.UtcNow, 1, null, null));

        var resolver = new ActivitySourceResolver([new ManualActivitySourceProvider(), replacement]);

        Assert.Same(replacement, resolver.Resolve(ActivitySource.Manual));
    }
}
