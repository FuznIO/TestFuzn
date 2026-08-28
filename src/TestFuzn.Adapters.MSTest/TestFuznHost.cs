using Fuzn.TestFuzn.Adapters;
using Fuzn.TestFuzn.StandaloneRunner;
using System.Reflection;

namespace Fuzn.TestFuzn;

/// <summary>
/// Hosts TestFuzn's standalone runner inside the test project's own entry point.
/// Use from a custom <c>Main</c> method (with <c>GenerateTestingPlatformEntryPoint</c>
/// set to <c>false</c>) to run tests with real-time console output without a separate
/// runner project, while all other invocations are forwarded to the MSTest runner.
/// </summary>
public static class TestFuznHost
{
    /// <summary>
    /// Determines whether the command-line arguments select the standalone runner:
    /// the first argument is the <c>run</c> verb. All other invocations
    /// (<c>dotnet test</c>, Test Explorer, CI) should be forwarded to the MSTest runner.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the test executable.</param>
    public static bool IsStandaloneRun(string[] args)
    {
        return args.Length > 0 && string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs a test using the standalone runner with real-time console output.
    /// The test is selected via <c>--test-name=&lt;name&gt;</c>, the <c>TESTFUZN_TEST_NAME</c>
    /// environment variable, or an interactive selection menu when neither is provided.
    /// </summary>
    /// <typeparam name="TStartup">The <see cref="IStartup"/> implementation to configure the suite.</typeparam>
    /// <param name="testAssembly">The assembly containing the test classes to discover and run.</param>
    /// <param name="args">Command-line arguments passed to the runner.</param>
    /// <returns>The process exit code: 0 when the test passes or is skipped, 1 when it fails.</returns>
    public static async Task<int> RunStandalone<TStartup>(Assembly testAssembly, string[] args)
        where TStartup : IStartup, new()
    {
        var runner = new StandaloneRunnerCore();
        return await runner.Run<TStartup>(testAssembly, args, () => new StandaloneRunnerAdapter());
    }
}
