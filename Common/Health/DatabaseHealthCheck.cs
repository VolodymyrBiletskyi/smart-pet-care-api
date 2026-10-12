using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using smart_pet_care_api.Data;

namespace smart_pet_care_api.Common.Health;

/// <summary>
/// Reports whether the api can reach its database.
/// </summary>
/// <remarks>
/// A process that is alive is not a process that can serve a request: when the
/// database refused every connection for 27 hours in October 2026, the api
/// answered this endpoint 200 throughout and both the container healthcheck and
/// the uptime probe stayed green while every request failed. Opening a
/// connection is the cheapest check that would not have been fooled, because
/// refusing to open one is exactly how that outage presented.
/// </remarks>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // CanConnectAsync answers false for a refused connection and throws for
        // some driver-level failures; the framework turns a throw into
        // Unhealthy, so both paths report the same thing.
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("the database is not reachable");
    }
}
