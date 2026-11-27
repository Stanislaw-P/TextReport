using Microsoft.Extensions.Options;
using TextReport.Features;
using TextReport.Infrastructure.Persistence;

namespace TextReport.Workers;

internal abstract class PollingWorker(
    IServiceScopeFactory scopeFactory,
    DatabaseGate databaseGate,
    DatabaseLane lane,
    IOptions<WorkerOptions> options,
    ILogger logger) : BackgroundService
{
    protected WorkerOptions Options { get; } = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var access = await databaseGate.EnterAsync(lane, stoppingToken);
                await using var scope = scopeFactory.CreateAsyncScope();
                await ProcessNextAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("{Worker} poll failed ({ErrorType})", GetType().Name, exception.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(Options.PollIntervalSeconds), stoppingToken);
        }
    }

    protected abstract Task ProcessNextAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected void RecordFailure(StageAttempts attempts, Guid orderId, Exception exception)
    {
        var error = exception.GetType().Name;
        DateTimeOffset? nextAttemptAt = attempts.Count + 1 < Options.MaxAttempts
            ? DateTimeOffset.UtcNow.AddSeconds(Options.RetryDelaySeconds)
            : null;
        attempts.RecordFailure(error, nextAttemptAt);
        logger.LogWarning("{Worker} failed for order {OrderId} on attempt {Attempt} ({ErrorType})",
            GetType().Name, orderId, attempts.Count, error);
    }
}
