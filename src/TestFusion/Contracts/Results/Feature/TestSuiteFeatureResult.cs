using System.Collections.Concurrent;

namespace TestFusion.Contracts.Results.Feature;

public class TestSuiteFeatureResult
{
    public ConcurrentDictionary<string, FeatureResult> FeatureResults { get; } = new();
}
