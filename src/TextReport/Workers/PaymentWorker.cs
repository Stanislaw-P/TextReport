using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TextReport.Features.Payments;
using TextReport.Infrastructure.Persistence;

namespace TextReport.Workers;

internal sealed class PaymentWorker(
    IServiceScopeFactory scopeFactory,
    DatabaseGate databaseGate,
    IOptions<WorkerOptions> options,
    ILogger<PaymentWorker> logger) : PollingWorker(scopeFactory, databaseGate, DatabaseLane.Payments, options, logger)
{
    protected override async Task ProcessNextAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<TextReportDbContext>();
        var now = DateTimeOffset.UtcNow;

        var payment = await db.Payments
            .Include(x => x.Order)
            .Where(x => x.Status == PaymentStatus.Pending && x.Attempts.Count < Options.MaxAttempts
                && (x.Attempts.NextAttemptAt == null || x.Attempts.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
            return;

        var paidAt = DateTimeOffset.UtcNow;
        payment.Status = PaymentStatus.Paid;
        payment.ProcessedAt = paidAt;
        payment.Order.PaidAt = paidAt;
        payment.Attempts.RecordSuccess();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Discard the proposed transition and only record a failure if payment is still pending
            db.ChangeTracker.Clear();
            var pending = await db.Payments.FirstOrDefaultAsync(
                x => x.Id == payment.Id && x.Status == PaymentStatus.Pending, cancellationToken);
            if (pending is null)
                return;

            RecordFailure(pending.Attempts, pending.OrderId, exception);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
