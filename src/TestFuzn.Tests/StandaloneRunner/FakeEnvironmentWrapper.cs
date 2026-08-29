using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn.Tests.StandaloneRunner;

/// <summary>
/// Test <see cref="IEnvironmentWrapper"/> over a dictionary of variables the test sets, so a
/// runner can be driven with a target environment without touching the process environment
/// that the tests running in parallel share; an unset variable reads as null, as the real
/// environment answers.
/// </summary>
internal sealed class FakeEnvironmentWrapper : IEnvironmentWrapper
{
    /// <summary>The variables set, by name.</summary>
    public Dictionary<string, string> Variables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string MachineName { get; set; } = "fake-machine";

    public string? GetEnvironmentVariable(string key)
    {
        if (Variables.TryGetValue(key, out var value))
            return value;

        return null;
    }

    public string GetMachineName()
    {
        return MachineName;
    }
}
