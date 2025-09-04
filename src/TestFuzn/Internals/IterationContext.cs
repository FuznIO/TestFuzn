using FuznLabs.TestFuzn.Contracts.Adapters;
using FuznLabs.TestFuzn.Contracts.Providers;
using FuznLabs.TestFuzn.Internals.Execution;

namespace FuznLabs.TestFuzn.Internals;

internal class IterationContext
{
    // Context
    public ExecutionInfo Info { get; internal set; }
    public ContextInternals Internals { get; set; }
    public ILogger Logger { get; set; }
    public ITestFrameworkAdapter TestFramework { get; internal set; }
    public HashSet<ISerializerProvider> SerializerProvider { get; set; }

    // StepContext
    public Scenario Scenario { get; internal set; }
    public Dictionary<string, object> SharedData { get; set; }
    public object InputData { get; set; }
    public ExecuteStepHandler ExecuteStepHandler { get; set; }
    public object Custom { get; set; }
}
