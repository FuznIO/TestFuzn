using System.Collections.Concurrent;

namespace TestFusion.Results.Feature;

public class TestSuiteResults
{
    public ConcurrentDictionary<string, FeatureResult> FeatureResults { get; } = new();
}
