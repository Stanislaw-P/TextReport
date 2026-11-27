using Microsoft.Extensions.Diagnostics.HealthChecks;
using TextReport.Infrastructure.Persistence;

namespace TextReport.Features.Health;

internal sealed class DatabaseHealthCheck(TextReportDbContext dbContext, DatabaseGate gate) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var access = await gate.EnterAsync(DatabaseLane.Requests, cancellationToken);
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
