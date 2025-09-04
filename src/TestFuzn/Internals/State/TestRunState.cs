namespace Fuzn.TestFuzn.Internals.State;

internal class TestRunState
{
    public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.NotStarted;
    public Exception ExecutionStoppedReason { get; set; }
    public Exception FirstException { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public TimeSpan TestRunDuration()
    {
        if (EndTime == default)
            return DateTime.UtcNow - StartTime;
        return EndTime - StartTime;
    }
}
