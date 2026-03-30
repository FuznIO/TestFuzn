using System.Collections.Concurrent;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Reports.Load;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Internals.State;

internal class TestExecutionState : IDisposable
{
    private volatile ExecutionStatus _executionStatus = ExecutionStatus.NotStarted;
    private CancellationTokenSource? _cancellationTokenSource;

    public ITestFrameworkAdapter TestFramework { get; private set; } = null!;
    public List<Scenario> Scenarios { get; private set; } = new();
    public ITest TestClassInstance { get; private set; } = null!;
    public TestResult TestResult { get; private set; } = null!;
    public TestType TestType => TestResult.TestType;
    public Dictionary<string, ScenarioLoadCollector> LoadCollectors = new();
    public InMemorySnapshotCollector LoadSnapshotCollector { get; } = new();
    public ExecutionStatus ExecutionStatus
    {
        get => _executionStatus;
        set => _executionStatus = value;
    }
    public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;
    public Exception? ExecutionStoppedReason { get; set; }
    public Exception? FirstException { get; set; }
    public BlockingCollection<ExecuteScenarioMessage> MessageQueue { get; } = new();
    public Dictionary<string, ConcurrentDictionary<Guid, bool>> ConstantMessageQueue { get; } = new();
    public ConcurrentDictionary<string, int> MessageCountPerScenario { get; } = new();
    public ConcurrentDictionary<string, bool> ProducersCompleted { get; } = new();
    public bool IsConsumingCompleted { get; private set; }
    public readonly ConcurrentDictionary<string, DateTime> LastSinkWrite = new();
    public readonly ConcurrentDictionary<string, SemaphoreSlim> SinkSemaphores = new();
    public TestSession TestSession { get; internal set; }

    public TestExecutionState(TestSession testSession)
    {
        TestSession = testSession;
    }

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
        
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(testFramework.CancellationToken);
        _cancellationTokenSource.Token.Register(() => _executionStatus = ExecutionStatus.Stopped);
        ExecutionStatus = ExecutionStatus.Running;
        TestClassInstance = test;
        Scenarios.AddRange(scenarios);

        foreach (var scenario in scenarios)
        {
            if (!ConstantMessageQueue.ContainsKey(scenario.Name))
                ConstantMessageQueue.TryAdd(scenario.Name, new ConcurrentDictionary<Guid, bool>());

            MessageCountPerScenario[scenario.Name] = 0;

            LoadCollectors.Add(scenario.Name, new ScenarioLoadCollector(scenario));
        }
    }

    public void EnqueueScenarioExecution(ExecuteScenarioMessage message)
    {
        MessageQueue.Add(message);

        MessageCountPerScenario.AddOrUpdate(message.ScenarioName, 1, (key, oldValue) => oldValue + 1);
    }

    public void AddToConstantQueue(ExecuteScenarioMessage message)
    {
        var queue = ConstantMessageQueue[message.ScenarioName];

        queue.TryAdd(message.MessageId, true);
    }

    public int GetConstantQueueCount(string scenarioName)
    {
        var queue = ConstantMessageQueue[scenarioName];
        return queue.Count;
    }

    public void RemoveFromQueues(ExecuteScenarioMessage message)
    {
        MessageCountPerScenario.AddOrUpdate(message.ScenarioName, 0, (key, oldValue) => oldValue - 1);

        var queue = ConstantMessageQueue[message.ScenarioName];
        queue.Remove(message.MessageId, out _);   
    }

    public void MarkScenarioProducersCompleted(string scenarioName)
    {
        ProducersCompleted.AddOrUpdate(scenarioName, key => true, (key, oldValue) => true);
    }

    public bool IsScenarioExecutionComplete(string scenarioName)
    {
        if (!ProducersCompleted.ContainsKey(scenarioName))
            return false;

        if (MessageCountPerScenario[scenarioName] == 0)
            return true;

        return false;
    }

    public bool IsExecutionQueueEmpty(string scenarioName)
    {
        if (MessageCountPerScenario[scenarioName] == 0)
            return true;

        return false;
    }

    public void MarkConsumingCompleted()
    {
        foreach (var scenario in Scenarios)
        {
            if (!ProducersCompleted.ContainsKey(scenario.Name))
                throw new InvalidOperationException($"Scenario '{scenario.Name}' producers have not been marked as completed.");
            if (MessageCountPerScenario[scenario.Name] > 0)
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

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
    }
}