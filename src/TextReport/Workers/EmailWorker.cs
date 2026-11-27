using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TextReport.Features.Notifications.SendEmail;
using TextReport.Infrastructure.Persistence;

namespace TextReport.Workers;

internal sealed class EmailWorker(
    IServiceScopeFactory scopeFactory,
    DatabaseGate databaseGate,
    IOptions<WorkerOptions> options,
    ILogger<EmailWorker> logger) : PollingWorker(scopeFactory, databaseGate, DatabaseLane.Email, options, logger)
{
    protected override async Task ProcessNextAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<TextReportDbContext>();
        var now = DateTimeOffset.UtcNow;

        var order = await db.Orders
            .Where(x => x.ProcessedAt != null && x.ResultJson != null && x.EmailSentAt == null
                && x.EmailAttempts.Count < Options.MaxAttempts
                && (x.EmailAttempts.NextAttemptAt == null || x.EmailAttempts.NextAttemptAt <= now))
            .OrderBy(x => x.ProcessedAt).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
            
        if (order is null)
            return;

        var sender = services.GetRequiredService<IEmailSender>();
        try
        {
            await sender.SendAsync(order.Id, order.Email, order.ResultJson!, cancellationToken);
            order.EmailSentAt = DateTimeOffset.UtcNow;
            order.EmailAttempts.RecordSuccess();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure(order.EmailAttempts, order.Id, exception);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
