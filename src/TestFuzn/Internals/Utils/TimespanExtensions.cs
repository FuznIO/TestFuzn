namespace Fuzn.TestFuzn.Internals.Utils;

internal static class TimespanExtensions
{
    public static string ToTestFuznResponseTime(this TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
            return "N/A";
        else if (duration.TotalMilliseconds < 1)
            return "<1 ms";
        return $"{duration.TotalMilliseconds:F0} ms";
    }

    public static string ToTestFuznFormattedDuration(this TimeSpan duration)
    {
        return $"{duration:hh\\:mm\\:ss\\:ff}";
    }

    public static string ToTestFuznReadableString(this TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
            return "0ms";

        var parts = new[]
        {
            duration.Days > 0 ? $"{duration.Days}d" : null,
            duration.Hours > 0 ? $"{duration.Hours}h" : null,
            duration.Minutes > 0 ? $"{duration.Minutes}m" : null,
            duration.Seconds > 0 ? $"{duration.Seconds}s" : null,
            duration.Milliseconds > 0 ? $"{duration.Milliseconds}ms" : null
        }.Where(part => part != null);

        return parts.Any() ? string.Join(" ", parts) : "0ms";
    }
}
