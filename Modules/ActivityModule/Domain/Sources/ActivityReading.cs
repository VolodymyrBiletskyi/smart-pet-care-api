namespace smart_pet_care_api.Modules.ActivityModule.Domain.Sources
{
    /// <summary>
    /// One activity measurement, already normalized (UTC instant, trimmed strings) but not
    /// yet validated or persisted. This is the only shape the service consumes, so a device
    /// provider and the manual one are interchangeable from its point of view.
    /// </summary>
    public sealed record ActivityReading(
        DateTime RecordedAt,
        int? Steps,
        string? Location,
        string? Note);
}
