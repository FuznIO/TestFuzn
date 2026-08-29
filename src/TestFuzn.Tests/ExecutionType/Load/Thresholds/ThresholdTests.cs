using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Thresholds;

/// <summary>
/// Runs real in-memory load tests (no TestWebApp) with declared thresholds and pins the
/// completion verdict: a passing verdict stored on the result with the scenario left passed,
/// a violated threshold failing the scenario through the assert-when-done pathway with the
/// exact violations message and the run left completed (not stopped), every violation listed
/// in declaration order, an AssertWhenDone failure keeping precedence, and a scenario without
/// thresholds behaving exactly as before.
/// </summary>
[TestClass]
public class ThresholdTests : Test
{
    /// <summary>What the run left behind, captured through the internal-state hook after cleanup and the summary.</summary>
    private sealed class CapturedState
    {
        public ScenarioLoadResult? Result;
        public ExecutionStatus? ExecutionStatus;
        public Exception? ExecutionStoppedReason;
        public Exception? FirstException;
        public TestStatus? TestStatus;

        public void Capture(AssertInternalState state, string scenarioName)
        {
            Result = state.TestExecutionState.LoadCollectors[scenarioName].GetCurrentResult(true);
            ExecutionStatus = state.TestExecutionState.ExecutionStatus;
            ExecutionStoppedReason = state.TestExecutionState.ExecutionStoppedReason;
            FirstException = state.TestExecutionState.FirstException;
            TestStatus = state.TestExecutionState.TestResult.Status;
        }
    }

    [Test]
    public async Task Verify_a_passing_verdict_is_stored_and_leaves_the_scenario_passed()
    {
        const string scenarioName = "Thresholds that hold";
        var captured = new CapturedState();
        AssertScenarioStats? statsWhenDone = null;

        await Scenario(scenarioName)
            .Step("Succeed", context => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Load().Thresholds(thresholds => thresholds
                .ResponseTimeMean(TimeSpan.FromSeconds(10))
                .ResponseTimePercentile95(TimeSpan.FromSeconds(10))
                .ResponseTimePercentile99(TimeSpan.FromSeconds(10))
                .ErrorRate(0)
                .RequestsPerSecond(minimum: 1))
            .Load().AssertWhenDone((context, stats) =>
            {
                statsWhenDone = stats;
            })
            .AssertInternalState(state => captured.Capture(state, scenarioName))
            .Run();

        Assert.IsNotNull(statsWhenDone);
        Assert.IsNotNull(captured.Result);
        Assert.AreEqual(TestStatus.Passed, captured.Result.Status);
        Assert.AreEqual(TestStatus.Passed, captured.TestStatus);
        Assert.IsNull(captured.Result.AssertWhenDoneException);
        Assert.IsNull(captured.FirstException);
        Assert.AreEqual(ExecutionStatus.Completed, captured.ExecutionStatus);

        var verdict = captured.Result.ThresholdResults;
        Assert.HasCount(5, verdict);
        foreach (var thresholdResult in verdict)
            Assert.IsTrue(thresholdResult.Passed, thresholdResult.Threshold.Metric.ToString());

        // The verdict reads the very numbers AssertWhenDone was given.
        Assert.AreEqual(ThresholdMetric.ResponseTimeMean, verdict[0].Threshold.Metric);
        Assert.AreEqual(statsWhenDone.Ok.ResponseTimeMean.TotalMilliseconds, verdict[0].Current);
        Assert.AreEqual(statsWhenDone.Ok.ResponseTimePercentile95.TotalMilliseconds, verdict[1].Current);
        Assert.AreEqual(statsWhenDone.Ok.ResponseTimePercentile99.TotalMilliseconds, verdict[2].Current);
        Assert.AreEqual(ThresholdMetric.ErrorRate, verdict[3].Threshold.Metric);
        Assert.AreEqual(0.0, verdict[3].Current);
        Assert.AreEqual(ThresholdMetric.RequestsPerSecond, verdict[4].Threshold.Metric);
        Assert.AreEqual(statsWhenDone.Ok.RequestsPerSecond, verdict[4].Current);
        Assert.AreEqual(10, statsWhenDone.Ok.RequestCount);
    }

    [Test]
    public async Task Verify_no_thresholds_declared_evaluates_nothing()
    {
        // Every iteration fails, so any error-rate threshold would trip — without one, nothing
        // is evaluated and the scenario ends exactly as before: passed, with no assert failure
        // and no threshold results.
        const string scenarioName = "No thresholds declared";
        var captured = new CapturedState();
        var executionCount = 0;

        await Scenario(scenarioName)
            .Step("Fail every iteration", context =>
            {
                if (Interlocked.Increment(ref executionCount) > 0)
                    throw new Exception("Simulated failure");
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .AssertInternalState(state => captured.Capture(state, scenarioName))
            .Run();

        Assert.IsNotNull(captured.Result);
        Assert.IsEmpty(captured.Result.ThresholdResults);
        Assert.AreEqual(TestStatus.Passed, captured.Result.Status);
        Assert.AreEqual(TestStatus.Passed, captured.TestStatus);
        Assert.IsNull(captured.Result.AssertWhenDoneException);
        Assert.IsNull(captured.FirstException);
        Assert.AreEqual(5, captured.Result.Failed.RequestCount);
        Assert.AreEqual(ExecutionStatus.Completed, captured.ExecutionStatus);
    }

    [Test]
    public async Task ShouldFail_Verify_a_violated_threshold_fails_the_scenario_with_the_violations_message()
    {
        const string scenarioName = "Error rate over its threshold";
        var captured = new CapturedState();
        var executionCount = 0;

        var exception = await Assert.ThrowsExactlyAsync<ThresholdViolationException>(() => Scenario(scenarioName)
            .Step("Fail every second iteration", context =>
            {
                if (Interlocked.Increment(ref executionCount) % 2 == 0)
                    throw new Exception("Simulated failure");
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Load().Thresholds(thresholds => thresholds.ErrorRate(0.01))
            .AssertInternalState(state => captured.Capture(state, scenarioName))
            .Run());

        // Five failed of ten: a 50 % error rate against the 1 % limit.
        Assert.AreEqual("Threshold violated: error rate 50 % > 1 %", exception.Message);
        var violation = Assert.ContainsSingle(exception.Violations);
        Assert.AreEqual(ThresholdMetric.ErrorRate, violation.Threshold.Metric);
        Assert.AreEqual(0.5, violation.Current);
        Assert.IsFalse(violation.Passed);
        Assert.AreEqual(10, executionCount);

        // The assert-when-done pathway: the scenario and the test result are failed, the
        // exception is the scenario's assert-when-done failure and the run's first exception —
        // the very instance the test observed.
        Assert.IsNotNull(captured.Result);
        Assert.AreEqual(TestStatus.Failed, captured.Result.Status);
        Assert.AreEqual(TestStatus.Failed, captured.TestStatus);
        Assert.AreSame(exception, captured.Result.AssertWhenDoneException);
        Assert.AreSame(exception, captured.FirstException);
        Assert.AreSame(violation, Assert.ContainsSingle(captured.Result.ThresholdResults));

        // A verdict never stops the run: it completed, with no stopped reason.
        Assert.AreEqual(ExecutionStatus.Completed, captured.ExecutionStatus);
        Assert.IsNull(captured.ExecutionStoppedReason);
    }

    [Test]
    public async Task ShouldFail_Verify_every_violation_is_listed_in_declaration_order()
    {
        const string scenarioName = "Two thresholds over";
        var captured = new CapturedState();
        var executionCount = 0;

        var exception = await Assert.ThrowsExactlyAsync<ThresholdViolationException>(() => Scenario(scenarioName)
            .Step("Take at least five milliseconds and fail every second iteration", async context =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5));
                if (Interlocked.Increment(ref executionCount) % 2 == 0)
                    throw new Exception("Simulated failure");
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Load().Thresholds(thresholds => thresholds
                .ResponseTimePercentile95(TimeSpan.FromMilliseconds(1))
                .ErrorRate(0.01)
                .RequestsPerSecond(minimum: 1))
            .AssertInternalState(state => captured.Capture(state, scenarioName))
            .Run());

        // The p95 of the successful iterations is at least the 5 ms they take, over the 1 ms
        // limit; the error rate is 50 %; the rate holds and is not listed.
        Assert.MatchesRegex(@"^Threshold violated: p95 \d+(\.\d)? ms > 1 ms; error rate 50 % > 1 %$", exception.Message);
        Assert.HasCount(2, exception.Violations);
        Assert.AreEqual(ThresholdMetric.ResponseTimePercentile95, exception.Violations[0].Threshold.Metric);
        Assert.AreEqual(ThresholdMetric.ErrorRate, exception.Violations[1].Threshold.Metric);

        Assert.IsNotNull(captured.Result);
        var verdict = captured.Result.ThresholdResults;
        Assert.HasCount(3, verdict);
        Assert.IsGreaterThanOrEqualTo(5.0, verdict[0].Current);
        Assert.IsFalse(verdict[0].Passed);
        Assert.AreEqual(0.5, verdict[1].Current);
        Assert.IsFalse(verdict[1].Passed);
        Assert.AreEqual(ThresholdMetric.RequestsPerSecond, verdict[2].Threshold.Metric);
        Assert.IsTrue(verdict[2].Passed);
        Assert.AreSame(exception, captured.Result.AssertWhenDoneException);
    }

    [Test]
    public async Task ShouldFail_Verify_an_assert_when_done_failure_keeps_precedence_over_a_violated_threshold()
    {
        const string scenarioName = "Assert when done and a threshold both fail";
        var captured = new CapturedState();
        var executionCount = 0;

        var exception = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => Scenario(scenarioName)
            .Step("Fail every iteration", context =>
            {
                if (Interlocked.Increment(ref executionCount) > 0)
                    throw new Exception("Simulated failure");
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(4))
            .Load().Thresholds(thresholds => thresholds.ErrorRate(0.5))
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.Fail("Assert when done failed");
            })
            .AssertInternalState(state => captured.Capture(state, scenarioName))
            .Run());

        Assert.Contains("Assert when done failed", exception.Message);

        // The assert failure stays the reported one; the verdict is still stored.
        Assert.IsNotNull(captured.Result);
        Assert.AreEqual(TestStatus.Failed, captured.Result.Status);
        Assert.AreSame(exception, captured.Result.AssertWhenDoneException);
        Assert.AreSame(exception, captured.FirstException);
        var thresholdResult = Assert.ContainsSingle(captured.Result.ThresholdResults);
        Assert.AreEqual(ThresholdMetric.ErrorRate, thresholdResult.Threshold.Metric);
        Assert.AreEqual(1.0, thresholdResult.Current);
        Assert.IsFalse(thresholdResult.Passed);
        Assert.AreEqual(ExecutionStatus.Completed, captured.ExecutionStatus);
        Assert.IsNull(captured.ExecutionStoppedReason);
    }

    [Test]
    public async Task ShouldFail_Verify_a_run_stopped_by_assert_while_running_takes_no_verdict()
    {
        const string scenarioName = "Stopped before the verdict";
        var captured = new CapturedState();
        var executionCount = 0;

        // A single iteration, so exactly one AssertWhileRunning check fails the run and every
        // record of the stop is that one exception.
        var exception = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => Scenario(scenarioName)
            .Step("Fail the iteration", context =>
            {
                if (Interlocked.Increment(ref executionCount) > 0)
                    throw new Exception("Simulated failure");
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(1))
            .Load().Thresholds(thresholds => thresholds.ErrorRate(0.01))
            .Load().AssertWhileRunning((context, stats) =>
            {
                Assert.AreEqual(0, stats.Failed.RequestCount, "Stop the run at the first failure");
            })
            .AssertInternalState(state => captured.Capture(state, scenarioName))
            .Run());

        Assert.Contains("Stop the run at the first failure", exception.Message);
        Assert.AreEqual(1, executionCount);

        // The assert-while-running failure stopped the run, so no verdict was taken: the
        // declared threshold has no result, as it has none on any stopped run.
        Assert.IsNotNull(captured.Result);
        Assert.AreEqual(ExecutionStatus.Stopped, captured.ExecutionStatus);
        Assert.AreSame(exception, captured.ExecutionStoppedReason);
        Assert.IsEmpty(captured.Result.ThresholdResults);
        Assert.AreSame(exception, captured.Result.AssertWhileRunningException);
        Assert.IsNull(captured.Result.AssertWhenDoneException);
        Assert.AreEqual(TestStatus.Failed, captured.Result.Status);
    }
}
