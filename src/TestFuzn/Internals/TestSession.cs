using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.Results.Standard;
using Microsoft.Extensions.Configuration;

namespace Fuzn.TestFuzn.Internals;

internal class TestSession
{
    private static readonly TestSession _default = new();
    private static readonly AsyncLocal<TestSession> _override = new();

    internal static TestSession Current
    {
        get => _override.Value ?? _default;
        set => _override.Value = value;
    }

    internal bool IsInitializeGlobalExecuted { get; set; } = false;
    internal string TestsOutputDirectory { get; set; }
    internal TestFuznConfiguration Configuration { get; set; }
    internal bool LoadTestWasExecuted { get; set; } = false;
    internal ILogger Logger { get; set; }
    internal string AssemblyWithTestsName { get; set; }
    internal string TestRunId { get; set; }
    internal DateTime TestRunStartTime { get; set; }
    internal DateTime TestRunEndTime { get; set; }
    internal TimeSpan SinkWriteFrequency { get; set; } = TimeSpan.FromSeconds(3);
    internal string NodeName { get; set; }
    internal LoggingVerbosity LoggingVerbosity => Configuration?.LoggingVerbosity ?? LoggingVerbosity.Full;
    internal string TargetEnvironment { get; set; }
    internal string ExecutionEnvironment { get; set; }
    internal List<string> TagsFilterInclude { get; set; } = new();
    internal List<string> TagsFilterExclude { get; set; } = new();
    internal StandardResultManager ResultManager { get; } = new();
    internal IStartup StartupInstance { get; set; }

    private IConfigurationRoot _configRoot;
    private readonly object _configLocker = new();

    internal IConfigurationRoot ConfigRoot
    {
        get => _configRoot ??= BuildConfigRoot();
        set => _configRoot = value;
    }

    internal void EnsureInitialized(ITestFrameworkAdapter testFramework)
    {
        if (testFramework == null)
            throw new ArgumentNullException(nameof(testFramework), "Test framework adapter cannot be null.");

        if (!IsInitializeGlobalExecuted)
            testFramework.ThrowTestFuznIsNotInitializedException();
    }

    private IConfigurationRoot BuildConfigRoot()
    {
        lock (_configLocker)
        {
            if (_configRoot != null)
                return _configRoot;

            var builder = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

            var executionEnv = ExecutionEnvironment;
            var targetEnv = TargetEnvironment;
            var nodeName = NodeName;

            if (!string.IsNullOrEmpty(executionEnv))
                builder.AddJsonFile($"appsettings.exec-{executionEnv}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(targetEnv))
                builder.AddJsonFile($"appsettings.target-{targetEnv}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(executionEnv) && !string.IsNullOrEmpty(targetEnv))
                builder.AddJsonFile($"appsettings.exec-{executionEnv}.target-{targetEnv}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(nodeName))
                builder.AddJsonFile($"appsettings.{nodeName}.json", optional: true, reloadOnChange: false);

            _configRoot = builder.Build();

            return _configRoot;
        }
    }

    internal static TestSession CreateIsolated()
    {
        return new TestSession();
    }
}
