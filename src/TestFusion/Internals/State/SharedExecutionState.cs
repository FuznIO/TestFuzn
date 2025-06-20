using System.Collections.Concurrent;
using TestFusion.Internals.Results.Feature;
using TestFusion.Results.Feature;

namespace TestFusion.Internals.State;

internal class SharedExecutionState
{
    private ResultsManager _resultsManager;
    public List<Scenario> Scenarios { get; set; } = new();
    public BlockingCollection<ScenarioExecutionInfo> ScenarioExecutionQueue { get; set; } = new();
    private Dictionary<string, ConcurrentDictionary<Guid, bool>> _constantQueue { get; set; } = new();
    private ConcurrentDictionary<string, int> _scenarioExecutionQueuedCount = new();
    private ConcurrentDictionary<string, bool> _producersCompleted = new();
    public bool IsConsumingCompleted { get; private set; }
    public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.NotStarted;
    public Exception ExecutionStoppedReason { get; set; }
    public Exception FirstException { get; set; }
    public ScenarioResult ScenarioResult { get; set; }
    public TestType TestType { get; set; }
    public IFeatureTest FeatureTestClassInstance { get; set; }

    public SharedExecutionState(ResultsManager resultsManager)
    {
        _resultsManager = resultsManager;
    }

    public void Init(IFeatureTest featureTest, params Scenario[] scenarios)
    {
        FeatureTestClassInstance = featureTest;

        if (scenarios.First().Simulations == null)
            TestType = TestType.Feature;
        else
            TestType = TestType.Load;

        ScenarioResult = _resultsManager.CreateScenarioResult(FeatureTestClassInstance.FeatureName, scenarios.First());

        ExecutionStatus = ExecutionStatus.Running;
        foreach (var scenario in scenarios)
        {
            Scenarios.Add(scenario);

            if (!_constantQueue.ContainsKey(scenario.Name))
                _constantQueue.TryAdd(scenario.Name, new ConcurrentDictionary<Guid, bool>());

            _scenarioExecutionQueuedCount[scenario.Name] = 0;
        }
    }

    public void EnqueueScenarioExecution(ScenarioExecutionInfo scenarioExecution)
    {
        ScenarioExecutionQueue.Add(scenarioExecution);

        _scenarioExecutionQueuedCount.AddOrUpdate(scenarioExecution.ScenarioName, 1, (key, oldValue) => oldValue + 1);
    }

    public void AddToConstantQueue(ScenarioExecutionInfo scenarioExecution)
    {
        var queue = _constantQueue[scenarioExecution.ScenarioName];

        queue.TryAdd(scenarioExecution.ExecutionId, true);
    }

    public int GetConstantQueueCount(string scenarioName)
    {
        var queue = _constantQueue[scenarioName];
        return queue.Count;
    }

    public void RemoveFromQueues(ScenarioExecutionInfo executionInfo)
    {
        _scenarioExecutionQueuedCount.AddOrUpdate(executionInfo.ScenarioName, 0, (key, oldValue) => oldValue - 1);

        var queue = _constantQueue[executionInfo.ScenarioName];
        queue.Remove(executionInfo.ExecutionId, out _);   
    }

    public void MarkScenarioProducersCompleted(string scenarioName)
    {
        _producersCompleted.AddOrUpdate(scenarioName, key => true, (key, oldValue) => true);
    }

    public bool IsScenarioExecutionComplete(string scenarioName)
    {
        if (!_producersCompleted.ContainsKey(scenarioName))
            return false;

        if (_scenarioExecutionQueuedCount[scenarioName] == 0)
            return true;

        return false;
    }

    public void MarkConsumingCompleted()
    {
        foreach (var scenario in Scenarios)
        {
            if (!_producersCompleted.ContainsKey(scenario.Name))
                throw new InvalidOperationException($"Scenario '{scenario.Name}' producers have not been marked as completed.");
            if (_scenarioExecutionQueuedCount[scenario.Name] > 0)
                throw new InvalidOperationException($"Scenario '{scenario.Name}' has pending executions in the queue.");
        }

        IsConsumingCompleted = true;
    }
}
