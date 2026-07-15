using Microsoft.Extensions.Options;

namespace smart_pet_care_api.Infrastructure.Classifier;

public readonly record struct ClassifierCircuitLease(
    bool IsAllowed,
    int? RetryAfterSeconds = null);

public sealed class ClassifierCircuitBreaker(
    IOptions<ClassifierOptions> options,
    TimeProvider timeProvider)
{
    private readonly object gate = new();
    private readonly int failureThreshold = options.Value.CircuitBreakerFailureThreshold;
    private readonly TimeSpan breakDuration = TimeSpan.FromSeconds(
        options.Value.CircuitBreakerBreakSeconds);
    private int consecutiveFailures;
    private DateTimeOffset? openUntil;
    private bool halfOpenProbeInProgress;

    public ClassifierCircuitLease TryAcquire()
    {
        lock (gate)
        {
            if (openUntil is null)
            {
                return new ClassifierCircuitLease(IsAllowed: true);
            }

            var remaining = openUntil.Value - timeProvider.GetUtcNow();
            if (remaining > TimeSpan.Zero)
            {
                return new ClassifierCircuitLease(
                    IsAllowed: false,
                    RetryAfterSeconds: ToRetryAfterSeconds(remaining));
            }

            if (halfOpenProbeInProgress)
            {
                return new ClassifierCircuitLease(
                    IsAllowed: false,
                    RetryAfterSeconds: 1);
            }

            halfOpenProbeInProgress = true;
            return new ClassifierCircuitLease(IsAllowed: true);
        }
    }

    public void RecordSuccess()
    {
        lock (gate)
        {
            consecutiveFailures = 0;
            openUntil = null;
            halfOpenProbeInProgress = false;
        }
    }

    public void RecordAvailabilityFailure()
    {
        lock (gate)
        {
            consecutiveFailures++;
            if (halfOpenProbeInProgress
                || consecutiveFailures >= failureThreshold)
            {
                openUntil = timeProvider.GetUtcNow() + breakDuration;
                halfOpenProbeInProgress = false;
            }
        }
    }

    public void RecordNeutralOutcome()
    {
        lock (gate)
        {
            halfOpenProbeInProgress = false;
        }
    }

    private static int ToRetryAfterSeconds(TimeSpan duration)
    {
        return duration.TotalSeconds >= int.MaxValue
            ? int.MaxValue
            : Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds));
    }
}
