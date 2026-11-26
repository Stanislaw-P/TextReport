namespace TextReport.Features;

internal sealed class StageAttempts
{
    public int Count { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public void RecordSuccess()
    {
        Count++;
        LastError = null;
        NextAttemptAt = null;
    }

    public void RecordFailure(string error, DateTimeOffset? nextAttemptAt)
    {
        Count++;
        LastError = error;
        NextAttemptAt = nextAttemptAt;
    }
}
