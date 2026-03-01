using System.Collections.Concurrent;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Internals.State;

internal class TestExecutionState
{
    public ITestFrameworkAdapter TestFramework { get; private set; } = null!;
    public List<Scenario> Scenarios { get; private set; } = new();
    public ITest TestClassInstance { get; private set; } = null!;
    public TestResult TestResult { get; private set; } = null!;
    public Dictionary<string, ScenarioLoadCollector> LoadCollectors = new();
    public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.NotStarted;
    public Exception? ExecutionStoppedReason { get; set; }
    public Exception? FirstException { get; set; }
    public ScenarioExecutionState ExecutionState { get; } = new();
    public bool IsConsumingCompleted { get; private set; }
    public readonly ConcurrentDictionary<string, DateTime> LastSinkWrite = new();
    public readonly ConcurrentDictionary<string, SemaphoreSlim> SinkSemaphores = new();

    public void Init(
        ITestFrameworkAdapter testFramework,
        ITest test, 
        params Scenario[] scenarios
        )
    {
        ArgumentNullException.ThrowIfNull(testFramework);
        ArgumentNullException.ThrowIfNull(test);
        if (scenarios == null || scenarios.Length == 0)
            throw new ArgumentException("At least one scenario must be provided.", nameof(scenarios));

        TestFramework = testFramework;
        TestResult = new TestResult(test.TestInfo, scenarios.First());
        TestResult.MarkPhaseAsStarted(StandardTestPhase.Init, DateTime.UtcNow);
        
        ExecutionStatus = ExecutionStatus.Running;
        TestClassInstance = test;
        Scenarios.AddRange(scenarios);

        foreach (var scenario in scenarios)
        {
            if (!ExecutionState.ConstantMessageQueue.ContainsKey(scenario.Name))
                ExecutionState.ConstantMessageQueue.TryAdd(scenario.Name, new ConcurrentDictionary<Guid, bool>());

            ExecutionState.MessageCountPerScenario[scenario.Name] = 0;

            LoadCollectors.Add(scenario.Name, new ScenarioLoadCollector(scenario));
        }
    }

    public void EnqueueScenarioExecution(ExecuteScenarioMessage message)
    {
        ExecutionState.MessageQueue.Add(message);

        ExecutionState.MessageCountPerScenario.AddOrUpdate(message.ScenarioName, 1, (key, oldValue) => oldValue + 1);
    }

    public void AddToConstantQueue(ExecuteScenarioMessage message)
    {
        var queue = ExecutionState.ConstantMessageQueue[message.ScenarioName];

        queue.TryAdd(message.MessageId, true);
    }

    public int GetConstantQueueCount(string scenarioName)
    {
        var queue = ExecutionState.ConstantMessageQueue[scenarioName];
        return queue.Count;
    }

    public void RemoveFromQueues(ExecuteScenarioMessage message)
    {
        ExecutionState.MessageCountPerScenario.AddOrUpdate(message.ScenarioName, 0, (key, oldValue) => oldValue - 1);

        var queue = ExecutionState.ConstantMessageQueue[message.ScenarioName];
        queue.Remove(message.MessageId, out _);   
    }

    public void MarkScenarioProducersCompleted(string scenarioName)
    {
        ExecutionState.ProducersCompleted.AddOrUpdate(scenarioName, key => true, (key, oldValue) => true);
    }

    public bool IsScenarioExecutionComplete(string scenarioName)
    {
        if (!ExecutionState.ProducersCompleted.ContainsKey(scenarioName))
            return false;

        if (ExecutionState.MessageCountPerScenario[scenarioName] == 0)
            return true;

        return false;
    }

    public bool IsExecutionQueueEmpty(string scenarioName)
    {
        if (ExecutionState.MessageCountPerScenario[scenarioName] == 0)
            return true;

        return false;
    }

    public void MarkConsumingCompleted()
    {
        foreach (var scenario in Scenarios)
        {
            if (!ExecutionState.ProducersCompleted.ContainsKey(scenario.Name))
                throw new InvalidOperationException($"Scenario '{scenario.Name}' producers have not been marked as completed.");
            if (ExecutionState.MessageCountPerScenario[scenario.Name] > 0)
                throw new InvalidOperationException($"Scenario '{scenario.Name}' has pending executions in the queue.");
        }

        IsConsumingCompleted = true;
    }

    public TimeSpan TestRunDuration()
    {
        var start = TestResult.StartTime();
        var end = TestResult.EndTime();

        if (end == default)
            return DateTime.UtcNow - start;

        return end - start;
    }
}