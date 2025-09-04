using FuznLabs.TestFuzn.Contracts.Adapters;
using FuznLabs.TestFuzn.Contracts.Providers;
using FuznLabs.TestFuzn.Internals;

namespace FuznLabs.TestFuzn;

public class Context
{
    internal IterationContext IterationContext { get; set; }

    public ExecutionInfo Info => IterationContext.Info;
    public ContextInternals Internals => IterationContext.Internals;
    public ILogger Logger => IterationContext.Logger; 
    public ITestFrameworkAdapter TestFramework => IterationContext.TestFramework;
    public HashSet<ISerializerProvider> SerializerProvider => IterationContext.SerializerProvider;
    public CurrentStep CurrentStep { get; internal set; }
}
