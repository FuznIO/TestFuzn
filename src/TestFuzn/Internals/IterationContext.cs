using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Providers;
using Fuzn.TestFuzn.Internals.Execution;

namespace Fuzn.TestFuzn.Internals;

internal class IterationState
{
    // Context
    public ExecutionInfo Info { get; internal set; }
    public ContextInternals Internals { get; set; }
    public ILogger Logger { get; set; }
    public ITestFrameworkAdapter TestFramework { get; internal set; }

    // Scenario / StepContext
    public Scenario Scenario { get; internal set; }
    public Dictionary<string, object> SharedData { get; set; }
    public object InputData { get; set; }
    public ExecuteStepHandler ExecuteStepHandler { get; set; }
    public object Model { get; set; }
}
