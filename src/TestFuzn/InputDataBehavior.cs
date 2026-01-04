namespace Fuzn.TestFuzn;

/// <summary>
/// Specifies how input data is consumed across iterations in a test scenario.
/// </summary>
public enum InputDataBehavior
{
    /// <summary>
    /// Iterates through input data sequentially.
    /// Standard test behavior: once the end of the data is reached, the test ends.
    /// Load tests behavior: once the end of the data is reached, it starts again from the beginning. Number of iterations are decided by the load test simulations.
    /// </summary>
    Loop = 1,

    /// <summary>
    /// Selects input data randomly for each iteration.
    /// Standard test behavior: Number of iterations are decided by the input data count.
    /// Load test behavior: Number of iterations are decided by the load test simulations.
    /// </summary>
    Random = 2,

    /// <summary>
    /// Load test behavior: Iterates through input data sequentially, then repeats the last item for all remaining iterations.
    /// Number of iterations are decided by the load test simulations.
    /// Standard test: Use Loop instead of LoopThenRepeatLast.
    /// </summary>
    LoopThenRepeatLast = 3,

    /// <summary>
    /// Load tests behavior: Iterates through input data sequentially, then selects randomly for remaining iterations.
    /// Useful for load tests where you want to ensure all data is used at least once before random selection.
    /// Number of iterations are decided by the load test simulations.
    /// Standard test: Use Loop or Random instead of LoopThenRandom.
    /// </summary>
    LoopThenRandom = 4
}
