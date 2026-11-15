using Fuzn.TestFuzn.Adapters;
using Fuzn.TestFuzn.Runner;
using Microsoft.Testing.Platform.Builder;
using System.Reflection;

namespace Fuzn.TestFuzn;

/// <summary>
/// Hosts both test runners behind the test project's entry point, and runs the suite's Init and Cleanup.
/// The Fuzn.TestFuzn.Adapters.MSTest package adds an entry point that calls <see cref="Run(string[])"/>.
/// To write your own <c>Main</c>, set <c>TestFuznGenerateEntryPoint</c> to <c>false</c> in the .csproj
/// and call <see cref="Run{TStartup}(string[])"/>.
/// </summary>
public static class TestFuznHost
{
    private const string RunnerPrefix = "--runner=";
    private const string TestFuznRunner = "testfuzn";
    private const string MsTestRunner = "mstest";
    private const string RunnerUsage = "--runner takes " + TestFuznRunner + " or " + MsTestRunner + ": " + RunnerPrefix + "<name>";

    /// <summary>
    /// Runs the entry assembly's tests with the one <see cref="IStartup"/> class in that assembly.
    /// </summary>
    public static Task<int> Run(string[] args)
    {
        var testAssembly = Assembly.GetEntryAssembly();
        if (testAssembly == null)
            throw new InvalidOperationException("No entry assembly to discover tests in.");

        var startupType = FindStartupType(testAssembly);
        var run = typeof(TestFuznHost).GetMethod(nameof(Run), 1, [typeof(string[])])!;
        return (Task<int>)run.MakeGenericMethod(startupType).Invoke(null, [args])!;
    }

    /// <summary>
    /// Runs the entry assembly's tests: TestFuzn's own runner for <c>--runner=testfuzn</c>,
    /// the MSTest runner otherwise.
    /// </summary>
    public static async Task<int> Run<TStartup>(string[] args)
        where TStartup : IStartup, new()
    {
        if (args == null)
            throw new ArgumentNullException(nameof(args), "Command-line arguments cannot be null.");

        var testAssembly = Assembly.GetEntryAssembly();
        if (testAssembly == null)
            throw new InvalidOperationException("No entry assembly to discover tests in.");

        var runnerArg = args.FirstOrDefault(a => a.StartsWith(RunnerPrefix, StringComparison.OrdinalIgnoreCase));
        var runner = runnerArg == null ? DefaultRunner(args) : runnerArg.Substring(RunnerPrefix.Length).Trim('\'', '"');

        // The MSTest runner rejects arguments it does not know.
        var remaining = runnerArg == null ? args : args.Where(a => a != runnerArg).ToArray();

        if (string.Equals(runner, TestFuznRunner, StringComparison.OrdinalIgnoreCase))
        {
            return await new TestFuznRunnerCore().Run<TStartup>(testAssembly, remaining, () => new TestFuznRunnerAdapter());
        }

        if (!string.Equals(runner, MsTestRunner, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(RunnerUsage);
            return 1;
        }

        if (TestFuznRunnerCore.IsHelpRequested(remaining))
            Console.WriteLine("For the TestFuzn runner's options: --runner=testfuzn --help" + Environment.NewLine);

        var builder = await TestApplication.CreateBuilderAsync(remaining);

        AddSelfRegisteredExtensions(testAssembly, builder, remaining);

        var sessionHandler = new MsTestSessionHandler((session, testFramework) => session.Init<TStartup>(testFramework));
        builder.TestHost.AddTestSessionLifetimeHandler(_ => sessionHandler);

        using var app = await builder.BuildAsync();
        var exitCode = await app.RunAsync();

        // 1 is the Testing Platform's generic failure code.
        return sessionHandler.Failed && exitCode == 0 ? 1 : exitCode;
    }

    private static Type FindStartupType(Assembly testAssembly)
    {
        var startupTypes = GetTypes(testAssembly)
            .Where(t => t.IsClass
                && !t.IsAbstract
                && !t.ContainsGenericParameters
                && typeof(IStartup).IsAssignableFrom(t)
                && t.GetConstructor(Type.EmptyTypes) != null)
            .ToList();

        var assemblyName = testAssembly.GetName().Name;

        if (startupTypes.Count == 0)
            throw new InvalidOperationException(
                $"No startup class found in '{assemblyName}'. Add a class that implements {nameof(IStartup)} " +
                "and has a public parameterless constructor.");

        if (startupTypes.Count > 1)
            throw new InvalidOperationException(
                $"More than one startup class found in '{assemblyName}': {string.Join(", ", startupTypes.Select(t => t.FullName))}. " +
                "Keep one, or set <TestFuznGenerateEntryPoint>false</TestFuznGenerateEntryPoint> in the .csproj " +
                "and call TestFuznHost.Run<YourStartup>(args) from your own Main.");

        return startupTypes[0];
    }

    // Worker apps start this exe as a load child or controller, and --cluster needs no MSTest;
    // the MSTest runner would reject their arguments.
    private static string DefaultRunner(string[] args)
    {
        string[] testFuznRunnerArguments = ["cluster-run", "cluster-controller", "cluster"];
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--"))
                continue;

            var name = arg.Substring(2).Split('=', 2)[0];
            if (testFuznRunnerArguments.Contains(name, StringComparer.OrdinalIgnoreCase))
                return TestFuznRunner;
        }

        return MsTestRunner;
    }

    private static void AddSelfRegisteredExtensions(Assembly testAssembly, ITestApplicationBuilder builder, string[] args)
    {
        var method = FindSelfRegisteredExtensionsMethod(testAssembly);
        if (method == null)
        {
            var assemblyName = testAssembly.GetName().Name;
            throw new InvalidOperationException(
                $"""
                No AddSelfRegisteredExtensions(ITestApplicationBuilder, string[]) method was generated into '{assemblyName}'.

                The MSTest runner generates it when EnableMSTestRunner is true. MSTest.Sdk sets it. Use MSTest.Sdk
                for the test project, or add this to the test project's .csproj file:

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <EnableMSTestRunner>true</EnableMSTestRunner>
                  </PropertyGroup>

                Then rebuild.
                """);
        }

        method.Invoke(null, [builder, args]);
    }

    // Looked up by signature: the generated type's name is not a stable contract.
    private static MethodInfo? FindSelfRegisteredExtensionsMethod(Assembly testAssembly)
    {
        foreach (var type in GetTypes(testAssembly))
        {
            var method = type.GetMethod(
                "AddSelfRegisteredExtensions",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: [typeof(ITestApplicationBuilder), typeof(string[])],
                modifiers: null);

            if (method != null)
                return method;
        }

        return null;
    }

    private static Type[] GetTypes(Assembly testAssembly)
    {
        try
        {
            return testAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>().ToArray();
        }
    }
}
