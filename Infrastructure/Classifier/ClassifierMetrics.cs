using System.Diagnostics.Metrics;

namespace smart_pet_care_api.Infrastructure.Classifier;

public sealed class ClassifierMetrics : IDisposable
{
    public const string MeterName = "SmartPetCare.Classifier";
    public const string FailureCounterName = "smart_pet_care.classifier.failures";

    private readonly Meter meter = new(MeterName);
    private readonly Counter<long> failures;

    public ClassifierMetrics()
    {
        failures = meter.CreateCounter<long>(
            FailureCounterName,
            description: "Classifier failures grouped by kind, status, code, and source.");
    }

    public void RecordFailure(
        string kind,
        int statusCode,
        string code,
        string source)
    {
        failures.Add(
            1,
            new KeyValuePair<string, object?>("kind", kind),
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("code", code),
            new KeyValuePair<string, object?>("source", source));
    }

    public void Dispose()
    {
        meter.Dispose();
    }
}
