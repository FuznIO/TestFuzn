using Fuzn.TestFuzn.Adapters;
using Fuzn.TestFuzn.StandaloneRunner;
using System.Reflection;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides a standalone runner for executing TestFuzn test suites outside of a MS test framework host.
/// </summary>
public class TestFuznStandaloneRunner
{
    /// <summary>
    /// Runs a standalone test using the specified <typeparamref name="TStartup"/> class.
    /// </summary>
    /// <typeparam name="TStartup">The <see cref="IStartup"/> implementation to configure the suite.</typeparam>
    public async Task Run<TStartup>(Assembly assembly, string[] args)
        where TStartup : IStartup, new()
    {
        var cli = new StandaloneRunnerCore();
        await cli.Run<TStartup>(assembly, args, () => new StandaloneRunnerAdapter());
    }
}
