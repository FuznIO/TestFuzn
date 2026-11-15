using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal static class LoadTarget
{
    private readonly record struct Piece(double Seconds, double? From, double? To);

    public static double? PerSecond(IReadOnlyList<ILoadConfiguration>? simulations, double fromSeconds, double toSeconds)
    {
        if (simulations == null || toSeconds <= fromSeconds
            || !simulations.Any(s => !s.IsWarmup && s is FixedLoadConfiguration or GradualLoadIncreaseConfiguration))
            return null;

        var planned = 0.0;
        var start = 0.0;
        foreach (var simulation in simulations.Where(s => !s.IsWarmup))
        {
            var piece = PieceOf(simulation);
            var end = start + piece.Seconds;
            var overlapFrom = Math.Max(start, fromSeconds);
            var overlapTo = Math.Min(end, toSeconds);
            var touches = piece.Seconds == 0
                ? start >= fromSeconds && start < toSeconds
                : overlapTo > overlapFrom;
            if (touches)
            {
                if (piece.From == null || piece.To == null)
                    return null;
                if (piece.Seconds > 0)
                    planned += Started(piece.From.Value, piece.To.Value, piece.Seconds, overlapFrom - start, overlapTo - start);
            }
            start = end;
        }

        return Math.Round(planned / (toSeconds - fromSeconds), 1);
    }

    private static double Started(double from, double to, double seconds, double a, double b)
    {
        var slope = (to - from) / seconds;
        return from * (b - a) + slope * (b * b - a * a) / 2;
    }

    private static Piece PieceOf(ILoadConfiguration simulation) => simulation switch
    {
        FixedLoadConfiguration f => new(f.Duration.TotalSeconds, f.Rate / f.Interval.TotalSeconds, f.Rate / f.Interval.TotalSeconds),
        GradualLoadIncreaseConfiguration g => new(g.Duration.TotalSeconds, g.StartRate, g.EndRate),
        PauseLoadConfiguration p => new(p.Duration.TotalSeconds, 0, 0),
        FixedConcurrentLoadConfiguration c when c.Duration > TimeSpan.Zero => new(c.Duration.TotalSeconds, null, null),
        FixedConcurrentLoadConfiguration => new(double.PositiveInfinity, null, null),
        RandomLoadPerSecondConfiguration r => new(r.Duration.TotalSeconds, null, null),
        _ => new(0, null, null)
    };
}
