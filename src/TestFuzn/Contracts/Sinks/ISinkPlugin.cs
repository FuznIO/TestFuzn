using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Contracts.Sinks;

internal interface ISinkPlugin
{
    Task InitSuite();
    Task WriteStats(SinkStats stats);
    Task CleanupSuite();
}
