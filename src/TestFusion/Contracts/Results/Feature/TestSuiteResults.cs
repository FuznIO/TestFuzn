using System.Collections.Concurrent;

namespace TestFusion.Contracts.Results.Feature;

public class TestSuiteResults
{
    public ConcurrentDictionary<string, FeatureResult> FeatureResults { get; } = new();
}
