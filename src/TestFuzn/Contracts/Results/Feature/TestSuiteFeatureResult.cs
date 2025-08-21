using System.Collections.Concurrent;

namespace FuznLabs.TestFuzn.Contracts.Results.Feature;

public class TestSuiteFeatureResult
{
    public ConcurrentDictionary<string, FeatureResult> FeatureResults { get; } = new();
}
