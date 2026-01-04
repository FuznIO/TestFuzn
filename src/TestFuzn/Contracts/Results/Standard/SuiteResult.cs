using System.Collections.Concurrent;

namespace Fuzn.TestFuzn.Contracts.Results.Standard;

internal class SuiteResult
{
    public ConcurrentDictionary<string, GroupResult> GroupResults { get; } = new();
}
