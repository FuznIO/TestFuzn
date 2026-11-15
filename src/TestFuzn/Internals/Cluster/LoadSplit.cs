namespace Fuzn.TestFuzn.Internals.Cluster;

internal static class LoadSplit
{
    // The leftover goes to the lowest indexes, the same rule as the input data slicing, so the parts
    // always add up to the total. Warmup gives every worker at least one, so each warms its own JIT and connections.
    public static int Part(int total, int workerIndex, int workerCount, bool isWarmup)
    {
        if (workerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(workerCount), workerCount, "A run needs at least one worker.");
        if (workerIndex < 0 || workerIndex >= workerCount)
            throw new ArgumentOutOfRangeException(nameof(workerIndex), workerIndex, $"The worker index must be between 0 and {workerCount - 1}.");
        if (total <= 0)
            return total;

        var part = total / workerCount + (workerIndex < total % workerCount ? 1 : 0);
        if (isWarmup && part == 0)
            return 1;

        return part;
    }
}
