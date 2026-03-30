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
    /// <param name="testAssembly">The assembly containing the test classes to discover and run.</param>
    /// <param name="args">Command-line arguments passed to the runner.</param>
    public async Task Run<TStartup>(Assembly testAssembly, string[] args)
        where TStartup : IStartup, new()
    {
        var cli = new StandaloneRunnerCore();
        await cli.Run<TStartup>(testAssembly, args, () => new StandaloneRunnerAdapter());
    }
}
