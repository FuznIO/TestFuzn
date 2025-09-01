using FuznLabs.TestFuzn.Contracts.Adapters;
using FuznLabs.TestFuzn.Contracts.Providers;

namespace FuznLabs.TestFuzn;

public class Context
{
    public string EnvironmentName { get; internal set; }
    public string NodeName { get; internal set; }
    public string TestRunId { get; internal set; }
    public ContextInternals Internals { get; set; }
    public ILogger Logger { get; set; }
    public CurrentStep CurrentStep { get; internal set; }
    public string CorrelationId { get; set; }
    public ITestFrameworkAdapter TestFramework { get; internal set; }
    public HashSet<ISerializerProvider> SerializerProvider { get; set; }
}