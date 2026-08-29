using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.Thresholds;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Execution;

internal class ExecutionManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TestExecutionState _testExecutionState;
    private readonly ProducerManager _producerManager;
    private readonly ConsumerManager _consumerManager;

    public ExecutionManager(
        IServiceProvider serviceProvider,
        TestExecutionState testExecutionState,
        ProducerManager producerManager,
        ConsumerManager consumerManager)
    {
        _serviceProvider = serviceProvider;
        _testExecutionState = testExecutionState;
        _producerManager = producerManager;
        _consumerManager = consumerManager;
    }

    public async Task Run()
    {
        _producerManager.StartProducers();

        _consumerManager.StartConsumers();

        try
        {
            await _producerManager.WaitForProducersToComplete();
        }
        catch (OperationCanceledException)
        {
            _testExecutionState.MessageQueue.CompleteAdding();
        }

        try
        {
            await _consumerManager.WaitForConsumersToFinish();
        }
        catch (OperationCanceledException)
        {
            // Consumer was cancelled — expected during test cancellation
        }

        ExecuteAssertWhenDone();

        _testExecutionState.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Execute, DateTime.UtcNow);
    }

    /// <summary>
    /// The completion verdicts of a load test, per scenario on the cumulative result: first the
    /// AssertWhenDone callback, then the declared thresholds (see <see cref="EvaluateThresholds"/>).
    /// Skipped for a standard test and for a stopped run — Ctrl+C, the quit key or an assert
    /// that stopped the run — as before: nothing is evaluated then, so a stopped run's threshold
    /// results stay empty.
    /// </summary>
    private void ExecuteAssertWhenDone()
    {
        if (_testExecutionState.TestResult.TestType == TestType.Standard)
            return;

        if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
            return;

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var scenarioCollector = _testExecutionState.LoadCollectors[scenario.Name];
            var scenarioResult = scenarioCollector.GetCurrentResult();
            var isAssertWhenDoneFailed = false;
            if (scenario.AssertWhenDoneAction != null)
            {
                try
                {
                    var context = ContextFactory.CreateScenarioContext(_testExecutionState.TestSession, _serviceProvider, _testExecutionState.TestFramework, "AssertWhenDoneAction", _testExecutionState.CancellationToken);
                    scenario.AssertWhenDoneAction(context, new AssertScenarioStats(scenarioResult));
                }
                catch (Exception e)
                {
                    isAssertWhenDoneFailed = true;
                    FailScenario(scenarioCollector, e);
                }
            }

            EvaluateThresholds(scenario, scenarioCollector, scenarioResult, isAssertWhenDoneFailed);
        }
    }

    /// <summary>
    /// The threshold verdict, at the same point and through the same failure pathway as the
    /// AssertWhenDone callback, on the same cumulative result: nothing happens for a scenario
    /// without thresholds — no evaluation, no result stored, no change in behaviour. Otherwise
    /// every threshold's verdict is stored on the collector (as
    /// <see cref="ScenarioLoadResult.ThresholdResults"/>, for the summaries and reports), and a
    /// violation fails the scenario the way an assert failure does — a
    /// <see cref="ThresholdViolationException"/> listing every violation becomes the run's first
    /// exception and the scenario's assert-when-done failure, so the scenario's status and
    /// status detail, the MSTest path and the standalone runner all report it alike; the
    /// execution status and stopped reason are left as they are, since the run completed. When
    /// the AssertWhenDone callback already failed the scenario, its failure stays the reported
    /// one and the verdict is only stored.
    /// </summary>
    private void EvaluateThresholds(Scenario scenario, ScenarioLoadCollector scenarioCollector, ScenarioLoadResult scenarioResult, bool isAssertWhenDoneFailed)
    {
        if (scenario.Thresholds.Count == 0)
            return;

        var thresholdResults = ThresholdEvaluator.EvaluateVerdict(scenario.Thresholds, scenarioResult);
        scenarioCollector.SetThresholdResults(thresholdResults);

        var violations = thresholdResults.Where(thresholdResult => !thresholdResult.Passed).ToList();
        if (violations.Count == 0 || isAssertWhenDoneFailed)
            return;

        FailScenario(scenarioCollector, new ThresholdViolationException(violations));
    }

    /// <summary>
    /// The assert-when-done failure pathway: the exception becomes the run's first exception —
    /// what the test runner rethrows once cleanup and the summary are done — and the test result
    /// is marked failed, and the scenario's collector records the exception (the status detail
    /// and the summaries' assert section) and the Failed status.
    /// </summary>
    private void FailScenario(ScenarioLoadCollector scenarioCollector, Exception exception)
    {
        _testExecutionState.FirstException = exception;
        _testExecutionState.TestResult.Status = TestStatus.Failed;
        scenarioCollector.SetAssertWhenDoneException(exception);
        scenarioCollector.SetStatus(TestStatus.Failed);
    }
}
