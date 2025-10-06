using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

public class Context
{
    internal IterationState IterationState { get; set; }

    public ExecutionInfo Info => IterationState.Info;
    public ContextInternals Internals => IterationState.Internals;
    public ILogger Logger => IterationState.Logger; 
    public ITestFrameworkAdapter TestFramework => IterationState.TestFramework;
    public StepInfo StepInfo { get; internal set; }
}
