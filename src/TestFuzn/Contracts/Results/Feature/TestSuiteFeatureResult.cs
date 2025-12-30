using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Contracts.Results.Feature;

internal class TestSuiteFeatureResult
{
    public ConcurrentDictionary<string, GroupResult> FeatureResults { get; } = new();
}
