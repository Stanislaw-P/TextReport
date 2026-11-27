using Microsoft.Extensions.Diagnostics.HealthChecks;
using TextReport.Infrastructure.Persistence;

namespace TextReport.Features.Health;

internal sealed class DatabaseHealthCheck(TextReportDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
