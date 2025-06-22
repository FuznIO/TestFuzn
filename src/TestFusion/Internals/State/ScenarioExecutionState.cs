using System.Collections.Concurrent;
using TestFusion.Internals.Execution;

namespace TestFusion.Internals.State;

internal class ScenarioExecutionState
{
    public BlockingCollection<ExecuteScenarioMessage> MessageQueue { get; set; } = new();
    public Dictionary<string, ConcurrentDictionary<Guid, bool>> ConstantMessageQueue { get; set; } = new();
    public ConcurrentDictionary<string, int> MessageCountPerScenario = new();
    public ConcurrentDictionary<string, bool> ProducersCompleted = new();
}
