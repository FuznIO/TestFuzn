namespace Fuzn.TestFuzn;

/// <summary>
/// Declares the thresholds of a load test scenario, from <see cref="LoadBuilder{TModel}.Thresholds"/>.
/// Each method adds one <see cref="Threshold"/> on a scenario-level statistic of the measurement
/// phase: the response-time thresholds are maxima over successful requests, the error-rate
/// threshold a maximum share of failed requests among all requests, and the request-rate
/// threshold a minimum. A metric can be declared once per scenario; declaring it again throws.
/// The thresholds are evaluated once when the load test completes, where
/// <see cref="LoadBuilder{TModel}.AssertWhenDone"/> runs, and a violation fails the test with
/// every violation listed (see <see cref="ThresholdViolationException"/>); the standalone
/// runner's live dashboard tracks them while the test runs.
/// </summary>
public sealed class ThresholdsBuilder
{
    private readonly List<Threshold> _thresholds;

    internal ThresholdsBuilder(List<Threshold> thresholds)
    {
        if (thresholds == null)
            throw new ArgumentNullException(nameof(thresholds), "Thresholds cannot be null.");

        _thresholds = thresholds;
    }

    /// <summary>
    /// Requires the mean response time of successful requests to stay at or below <paramref name="maximum"/>.
    /// </summary>
    /// <param name="maximum">The highest acceptable mean response time; cannot be negative.</param>
    /// <returns>The current <see cref="ThresholdsBuilder"/> instance for method chaining.</returns>
    public ThresholdsBuilder ResponseTimeMean(TimeSpan maximum)
    {
        return AddResponseTime(ThresholdMetric.ResponseTimeMean, maximum);
    }

    /// <summary>
    /// Requires the 95th-percentile response time of successful requests to stay at or below <paramref name="maximum"/>.
    /// </summary>
    /// <param name="maximum">The highest acceptable 95th-percentile response time; cannot be negative.</param>
    /// <returns>The current <see cref="ThresholdsBuilder"/> instance for method chaining.</returns>
    public ThresholdsBuilder ResponseTimePercentile95(TimeSpan maximum)
    {
        return AddResponseTime(ThresholdMetric.ResponseTimePercentile95, maximum);
    }

    /// <summary>
    /// Requires the 99th-percentile response time of successful requests to stay at or below <paramref name="maximum"/>.
    /// </summary>
    /// <param name="maximum">The highest acceptable 99th-percentile response time; cannot be negative.</param>
    /// <returns>The current <see cref="ThresholdsBuilder"/> instance for method chaining.</returns>
    public ThresholdsBuilder ResponseTimePercentile99(TimeSpan maximum)
    {
        return AddResponseTime(ThresholdMetric.ResponseTimePercentile99, maximum);
    }

    /// <summary>
    /// Requires the share of failed requests among all requests to stay at or below
    /// <paramref name="maximum"/>: a fraction from 0 (no failure tolerated) to 1 — 0.01 allows
    /// one failed request in a hundred.
    /// </summary>
    /// <param name="maximum">The highest acceptable error rate, from 0 to 1.</param>
    /// <returns>The current <see cref="ThresholdsBuilder"/> instance for method chaining.</returns>
    public ThresholdsBuilder ErrorRate(double maximum)
    {
        if (double.IsNaN(maximum) || maximum < 0 || maximum > 1)
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "The error rate maximum is a fraction from 0 to 1.");

        Add(new Threshold(ThresholdMetric.ErrorRate, maximum, ThresholdComparison.LessThanOrEqualTo));
        return this;
    }

    /// <summary>
    /// Requires the request rate to reach at least <paramref name="minimum"/> requests per second.
    /// The verdict measures the cumulative <c>Stats.RequestsPerSecond</c> of the successful
    /// measurement requests: an integer rate whose clock runs from the start of measurement to
    /// the last successful request — the same number <see cref="AssertStats.RequestsPerSecond"/>
    /// exposes on <see cref="AssertScenarioStats.Ok"/> and the summary prints as the OK rate.
    /// The standalone runner's live state, by contrast, reads each one-second interval's total
    /// rate, successful and failed requests alike.
    /// </summary>
    /// <param name="minimum">The lowest acceptable number of requests per second; cannot be negative.</param>
    /// <returns>The current <see cref="ThresholdsBuilder"/> instance for method chaining.</returns>
    public ThresholdsBuilder RequestsPerSecond(double minimum)
    {
        if (double.IsNaN(minimum) || double.IsInfinity(minimum) || minimum < 0)
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "The requests per second minimum cannot be negative.");

        Add(new Threshold(ThresholdMetric.RequestsPerSecond, minimum, ThresholdComparison.GreaterThanOrEqualTo));
        return this;
    }

    private ThresholdsBuilder AddResponseTime(ThresholdMetric metric, TimeSpan maximum)
    {
        if (maximum < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "A response time maximum cannot be negative.");

        Add(new Threshold(metric, maximum.TotalMilliseconds, ThresholdComparison.LessThanOrEqualTo));
        return this;
    }

    private void Add(Threshold threshold)
    {
        if (_thresholds.Any(existing => existing.Metric == threshold.Metric))
            throw new InvalidOperationException($"A threshold on {threshold.Metric} is already declared for the scenario.");

        _thresholds.Add(threshold);
    }
}
