using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TextReport.Features.Orders.ProcessOrder;
using TextReport.Infrastructure.Persistence;

namespace TextReport.Workers;

internal sealed class OrderWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    ILogger<OrderWorker> logger) : PollingWorker(scopeFactory, options, logger)
{
    protected override async Task ProcessNextAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<TextReportDbContext>();
        var now = DateTimeOffset.UtcNow;
        
        var order = await db.Orders
            .Where(x => x.PaidAt != null && x.ProcessedAt == null
                && x.ProcessingAttempts.Count < Options.MaxAttempts
                && (x.ProcessingAttempts.NextAttemptAt == null || x.ProcessingAttempts.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
            return;

        var processor = services.GetRequiredService<IOrderProcessor>();
        try
        {
            order.ResultJson = await processor.ProcessAsync(order.InputText, cancellationToken);
            order.ProcessedAt = DateTimeOffset.UtcNow;
            order.ProcessingAttempts.RecordSuccess();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure(order.ProcessingAttempts, order.Id, exception);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
