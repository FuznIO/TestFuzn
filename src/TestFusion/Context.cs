using TestFusion.Contracts.Adapters;
using TestFusion.Contracts.Providers;

namespace TestFusion;

public class Context
{
    public string EnvironmentName { get; internal set; }
    public string NodeName { get; internal set; }
    public string TestRunId { get; internal set; }
    public ContextInternals Internals { get; internal set; }
    public ILogger Logger { get; set; }
    public CurrentStep Step { get; internal set; }
    public string CorrelationId { get; set; }
    public ITestFrameworkAdapter TestFramework { get; internal set; }
    public HashSet<ISerializerProvider> SerializerProvider { get; set; }
}

