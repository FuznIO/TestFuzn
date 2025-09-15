using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Providers;
using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

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
