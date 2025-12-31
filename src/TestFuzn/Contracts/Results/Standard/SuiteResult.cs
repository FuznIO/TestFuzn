using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Contracts.Results.Feature;

internal class SuiteResult
{
    public ConcurrentDictionary<string, GroupResult> GroupResults { get; } = new();
}
