using System.Collections.Concurrent;
using TestFusion.Contracts.Results.Feature;
using TestFusion.Internals.Execution;
using TestFusion.Internals.Results.Load;

namespace TestFusion.Internals.State;

internal class SharedExecutionState
{
    public List<Scenario> Scenarios { get; set; } = new();
    public IFeatureTest IFeatureTestClassInstance { get; set; }
    public string FeatureName { get; set; }
    public TestType TestType { get; set; }
    public TestRunState TestRunState { get; } = new();
    public ScenarioExecutionState ExecutionState { get; } = new();
    public ScenarioResultState ResultState { get; } = new();

    public bool IsConsumingCompleted { get; private set; }

    public SharedExecutionState(IFeatureTest featureTest, params Scenario[] scenarios)
    {
        TestRunState.StartTime = DateTime.UtcNow;
        TestRunState.ExecutionStatus = ExecutionStatus.Running;
        IFeatureTestClassInstance = featureTest;
        FeatureName = featureTest.FeatureName;
        Scenarios.AddRange(scenarios);

        if (scenarios.First().SimulationsAction == null)
            TestType = TestType.Feature;
        else
            TestType = TestType.Load;

        foreach (var scenario in scenarios)
        {
            if (!ExecutionState.ConstantMessageQueue.ContainsKey(scenario.Name))
                ExecutionState.ConstantMessageQueue.TryAdd(scenario.Name, new ConcurrentDictionary<Guid, bool>());

            ExecutionState.MessageCountPerScenario[scenario.Name] = 0;

            ResultState.LoadCollectors.Add(scenario.Name, new ScenarioLoadCollector(scenario));
            ResultState.FeatureCollectors.Add(scenario.Name, new ScenarioFeatureResult(scenario));
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

    public void Complete()
    {
        TestRunState.EndTime = DateTime.UtcNow;

        foreach (var scenario in Scenarios)
        {
            ResultState.LoadCollectors[scenario.Name].MarkPhaseAsCompleted(LoadTestPhase.Measurement);
        }
    }
}
