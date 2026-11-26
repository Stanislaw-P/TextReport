using System.ComponentModel.DataAnnotations;

namespace TextReport.Workers;

internal sealed class WorkerOptions
{
    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 2;

    [Range(1, 86400)]
    public int RetryDelaySeconds { get; init; } = 10;

    [Range(1, 100)]
    public int MaxAttempts { get; init; } = 3;
}
