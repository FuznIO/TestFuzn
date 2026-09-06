using Fuzn.TestFuzn.Adapters;
using Fuzn.TestFuzn.Runner;
using Microsoft.Testing.Platform.Builder;
using System.Reflection;

namespace Fuzn.TestFuzn;

/// <summary>
/// Hosts both test runners behind the test project's own entry point. Use from a custom
/// <c>Main</c> method with <c>GenerateTestingPlatformEntryPoint</c> set to <c>false</c>.
/// </summary>
public static class TestFuznHost
{
    private const string RunnerPrefix = "--runner=";
    private const string TestFuznRunner = "testfuzn";
    private const string MsTestRunner = "mstest";
    private const string RunnerUsage = "--runner takes " + TestFuznRunner + " or " + MsTestRunner + ": " + RunnerPrefix + "<name>";

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
        var runner = runnerArg == null ? MsTestRunner : runnerArg.Substring(RunnerPrefix.Length).Trim('\'', '"');

        // The MSTest runner rejects arguments it does not know.
        var remaining = runnerArg == null ? args : args.Where(a => a != runnerArg).ToArray();

        if (string.Equals(runner, TestFuznRunner, StringComparison.OrdinalIgnoreCase))
        {
            await new TestFuznRunnerCore().Run<TStartup>(testAssembly, remaining, () => new TestFuznRunnerAdapter());
            return 0;
        }

        if (!string.Equals(runner, MsTestRunner, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(RunnerUsage);
            return 1;
        }

        var builder = await TestApplication.CreateBuilderAsync(remaining);

        AddSelfRegisteredExtensions(testAssembly, builder, remaining);

        using var app = await builder.BuildAsync();
        return await app.RunAsync();
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

                The MSTest runner generates it from the EnableMSTestRunner property. Add it to the
                test project's .csproj file, together with the entry point properties this host needs:

                  <Project Sdk="MSTest.Sdk">
                    <PropertyGroup>
                      <OutputType>Exe</OutputType>
                      <EnableMSTestRunner>true</EnableMSTestRunner>
                      <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
                    </PropertyGroup>
                  </Project>

                Then rebuild.
                """);
        }

        method.Invoke(null, [builder, args]);
    }

    // Looked up by signature: the generated type's name is not a stable contract.
    private static MethodInfo? FindSelfRegisteredExtensionsMethod(Assembly testAssembly)
    {
        Type[] types;
        try
        {
            types = testAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.OfType<Type>().ToArray();
        }

        foreach (var type in types)
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
}
