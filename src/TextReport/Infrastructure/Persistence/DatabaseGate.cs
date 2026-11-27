namespace TextReport.Infrastructure.Persistence;

internal enum DatabaseLane
{
    Requests,
    Payments,
    Processing,
    Email
}

// Coordinates work within the single supported application instance.
internal sealed class DatabaseGate : IDisposable
{
    private readonly SemaphoreSlim[] lanes = Enum.GetValues<DatabaseLane>()
        .Select(_ => new SemaphoreSlim(1))
        .ToArray();

    public async Task<IDisposable> EnterAsync(DatabaseLane lane, CancellationToken cancellationToken)
    {
        var semaphore = lanes[(int)lane];
        await semaphore.WaitAsync(cancellationToken);
        return new Lease(semaphore);
    }

    public void Dispose()
    {
        foreach (var lane in lanes)
            lane.Dispose();
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
