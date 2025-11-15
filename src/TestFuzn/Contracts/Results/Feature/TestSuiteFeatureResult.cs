using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Contracts.Results.Feature;

public class TestSuiteFeatureResult
{
    public ConcurrentDictionary<string, FeatureResult> FeatureResults { get; } = new();
}
