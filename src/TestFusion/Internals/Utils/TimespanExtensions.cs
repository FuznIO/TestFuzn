namespace TestFusion.Internals.Utils;

internal static class TimespanExtensions
{
    internal static string ToTestFusionResponseTime(this TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
            return "N/A";
        else if (duration.TotalMilliseconds < 1)
            return "<1 ms";
        return $"{duration.TotalMilliseconds:F0} ms";
    }
    internal static string ToTestFusionFormattedDuration(this TimeSpan duration)
    {
        return $"{duration:hh\\:mm\\:ss\\:ff}";
    }
}
