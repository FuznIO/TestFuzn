using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Internals.Cleanup;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Internals.AppConfiguration;
using Fuzn.TestFuzn.Internals.Init;
using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.Reports.EmbeddedResources;
using Fuzn.TestFuzn.Internals.Reports.Load;
using Fuzn.TestFuzn.Internals.Reports.Standard;
using Fuzn.TestFuzn.Internals.Results.Standard;
using Fuzn.TestFuzn.Internals.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Internals;

internal class TestSession
{
    private const string MarkerFileName = ".testfuzn";
    private const int DefaultKeepLastNRuns = 10;

    private static readonly AsyncLocal<TestSession> _current = new();
    private static TestSession? _default;

    private IFileSystem? _fileSystem;
    private int _keepLastNRuns = DefaultKeepLastNRuns;

    internal static TestSession? Current
    {
        get => _current.Value ?? _default;
        set => _current.Value = value;
    }

    internal static TestSession? Default
    {
        get
        {
            return _default;
        }
        set
        {
            _default = value;
        }
    }

    internal bool IsInitializeGlobalExecuted { get; set; } = false;
    internal string TestsResultsDirectory { get; set; }
    internal TestFuznConfiguration Configuration { get; set; }
    internal bool LoadTestWasExecuted { get; set; } = false;
    internal ILogger Logger { get; set; }
    internal string TestRunId { get; set; }
    internal DateTime TestRunStartTime { get; set; }
    internal DateTime TestRunEndTime { get; set; }
    internal TimeSpan SinkWriteFrequency { get; set; } = TimeSpan.FromSeconds(3);
    internal string NodeName { get; set; }
    internal StandardResultManager ResultManager { get; } = new();
    internal IStartup StartupInstance { get; set; }
    internal string Id { get; }

    internal IServiceProvider ServiceProvider { get; set; }

    internal TestSession(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    internal void EnsureInitialized(ITestFrameworkAdapter testFramework)
    {
        if (testFramework == null)
            throw new ArgumentNullException(nameof(testFramework), "Test framework adapter cannot be null.");

        if (!IsInitializeGlobalExecuted)
            testFramework.ThrowTestFuznIsNotInitializedException();
    }

    public async Task Init<TStartup>(
        ITestFrameworkAdapter testFramework,
        Dictionary<string, string>? args = null)
        where TStartup : IStartup, new()
    {
        var environmentWrapper = new EnvironmentWrapper();

        var fileSystem = new FileSystem();
        await Init<TStartup>(
            environmentWrapper,
            fileSystem,
            new ConfigurationLoader(),
            new ArgumentsParser(environmentWrapper),
            testFramework,
            args);
    }

    internal async Task Init<TStartup>(
        IEnvironmentWrapper environmentWrapper,
        IFileSystem fileSystem,
        IConfigurationLoader configurationLoader,
        ArgumentsParser argumentParser,
        ITestFrameworkAdapter testFramework,
        Dictionary<string, string>? args = null)
        where TStartup : IStartup, new()
    {
        StartupInstance = new TStartup();
        TestRunStartTime = DateTime.UtcNow;
        TestRunId = $"{DateTime.Now:yyyy-MM-dd_HH-mm}__{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        var targetEnvironment = argumentParser.GetValueFromArgsOrEnvironmentVariable(
                                        args, "target-environment", "TESTFUZN_TARGET_ENVIRONMENT");
        var executionEnvironment = argumentParser.GetValueFromArgsOrEnvironmentVariable(
                                        args, "execution-environment", "TESTFUZN_EXECUTION_ENVIRONMENT");

        var tagsFilterInclude = new List<string>();
        var tagsInclude = argumentParser.GetValueFromArgsOrEnvironmentVariable(
                                args, "tags-filter-include", "TESTFUZN_TAGS_FILTER_INCLUDE");
        if (!string.IsNullOrEmpty(tagsInclude))
        {
            tagsFilterInclude.AddRange(tagsInclude.Split(',').Select(t => t.Trim()));
        }

        var tagsFilterExclude = new List<string>();
        var tagsExclude = argumentParser.GetValueFromArgsOrEnvironmentVariable(args, "tags-filter-exclude", "TESTFUZN_TAGS_FILTER_EXCLUDE");
        if (!string.IsNullOrEmpty(tagsExclude))
        {
            tagsFilterExclude.AddRange(tagsExclude.Split(',').Select(t => t.Trim()));
        }

        NodeName = environmentWrapper.GetMachineName();

        var testAssemblyName = StartupInstance.GetType().Assembly.GetName().Name;

        var customResultsDirectory = argumentParser.GetValueFromArgsOrEnvironmentVariable(
                                        args, "results-directory", "TESTFUZN_RESULTS_DIRECTORY");
        TestsResultsDirectory = !string.IsNullOrWhiteSpace(customResultsDirectory)
            ? Path.Combine(customResultsDirectory, testAssemblyName, $"{TestRunId}")
            : Path.Combine(testFramework.TestResultsDirectory, "TestFuznResults", testAssemblyName, $"{TestRunId}");
        if (Id != "default")
            TestsResultsDirectory = TestsResultsDirectory + "_" + Id;

        fileSystem.CreateDirectory(TestsResultsDirectory);
        await fileSystem.WriteAllTextAsync(Path.Combine(TestsResultsDirectory, MarkerFileName), "");

        _fileSystem = fileSystem;
        var keepLastNRunsValue = argumentParser.GetValueFromArgsOrEnvironmentVariable(
                                        args, "keep-last-n-runs", "TESTFUZN_KEEP_LAST_N_RUNS");
        if (!string.IsNullOrWhiteSpace(keepLastNRunsValue) && int.TryParse(keepLastNRunsValue, out var parsed) && parsed >= 0)
            _keepLastNRuns = parsed;

        Logger = Internals.Logging.LoggerFactory.CreateLogger(fileSystem, TestsResultsDirectory);
        Logger.LogInformation("Logging initialized");

        var configRoot = configurationLoader.LoadConfigRoot(
                                executionEnvironment: executionEnvironment,
                                targetEnvironment: targetEnvironment,
                                nodeName: NodeName);

        var configurationManager = new AppConfigurationManager(configRoot);

        var services = new ServiceCollection();
        AddServices(services, fileSystem, Logger, configurationManager);

        var configuration = new TestFuznConfiguration(services);
        configuration.AppConfiguration = configurationManager;
        configuration.Suite = new SuiteInfo();
        configuration.Suite.Name = testAssemblyName;
        configuration.TargetEnvironment = targetEnvironment;
        configuration.ExecutionEnvironment = executionEnvironment;
        configuration.TagsFilterInclude = tagsFilterInclude;
        configuration.TagsFilterExclude = tagsFilterExclude;
        StartupInstance.Configure(configuration);
        Configuration = configuration;

        // Build the service provider after all plugins have registered their services
        ServiceProvider = configuration.BuildServiceProvider();

        if (StartupInstance is IBeforeSuite initGlobalInstance)
        {
            var context = ContextFactory.CreateContext(this, ServiceProvider, testFramework, "Init");
            await initGlobalInstance.BeforeSuite(context);
        }

        foreach (var plugin in ServiceProvider.GetServices<ISinkPlugin>())
            await plugin.InitSuite();

        foreach (var plugin in Configuration.ContextPlugins)
            await plugin.InitSuite();

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(fileSystem, "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Styles.testfuzn.css",
                                        Path.Combine(TestsResultsDirectory, "Data/Assets/styles/testfuzn.css"));

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(fileSystem, "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Scripts.chart.js",
                                        Path.Combine(TestsResultsDirectory, "Data/Assets/scripts/chart.js"));

        IsInitializeGlobalExecuted = true;
    }

    private void AddServices(ServiceCollection services,
        IFileSystem fileSystem,
        ILogger logger,
        AppConfigurationManager configurationManager)
    {
        services.AddSingleton(this);
        services.AddSingleton<IFileSystem>(fileSystem);
        services.AddSingleton<ILogger>(logger);
        services.AddSingleton(new FileManager(fileSystem));
        services.AddSingleton(configurationManager);
        services.AddScoped<TestRunner>();
        services.AddScoped<TestExecutionState>();
        services.AddScoped<ConsoleWriter>();
        services.AddScoped<ConsoleManager>();
        services.AddScoped<InitManager>();
        services.AddScoped<InputDataFeeder>();
        services.AddScoped<ProducerManager>();
        services.AddScoped<ExecuteScenarioMessageHandler>();
        services.AddScoped<ConsumerManager>();
        services.AddScoped<ExecutionManager>();
        services.AddScoped<CleanupManager>();
        services.AddScoped<StandardReportManager>();
        services.AddScoped<LoadReportManager>();
        services.AddSingleton<IStandardReport, StandardXmlReportWriter>();
        services.AddSingleton<IStandardReport, StandardHtmlReportWriter>();
        services.AddScoped<ILoadReport, LoadHtmlReportWriter>();
        services.AddScoped<ILoadReport, LoadXmlReportWriter>();
    }

    public async Task Cleanup(ITestFrameworkAdapter testFramework)
    {
        if (StartupInstance == null)
            throw new InvalidOperationException("TestFuznIntegration has not been initialized. Please call TestFuznIntegration.InitSuite() before running tests.");

        if (StartupInstance is IAfterSuite cleanupGlobalInstance)
        {
            await cleanupGlobalInstance.AfterSuite(ContextFactory.CreateContext(this, ServiceProvider, testFramework, "Cleanup"));
        }

        TestRunEndTime = DateTime.UtcNow;

        if (!IsInitializeGlobalExecuted)
            return;

        var standardReportManager = ServiceProvider.GetRequiredService<StandardReportManager>();
        await standardReportManager.WriteStandardReports(ResultManager);

        foreach (var plugin in Configuration.ContextPlugins)
            await plugin.CleanupSuite();

        foreach (var plugin in ServiceProvider.GetServices<ISinkPlugin>())
            await plugin.CleanupSuite();

        CleanupOldRuns();
    }

    private void CleanupOldRuns()
    {
        if (_fileSystem == null || _keepLastNRuns <= 0)
            return;

        var parentDirectory = Path.GetDirectoryName(TestsResultsDirectory);
        if (parentDirectory == null || !_fileSystem.DirectoryExists(parentDirectory))
            return;

        try
        {
            var runDirectories = _fileSystem.GetDirectories(parentDirectory)
                .Where(dir => _fileSystem.FileExists(Path.Combine(dir, MarkerFileName)))
                .OrderByDescending(dir => _fileSystem.GetDirectoryCreationTimeUtc(dir))
                .ToList();

            foreach (var dir in runDirectories.Skip(_keepLastNRuns))
            {
                try
                {
                    _fileSystem.DeleteDirectory(dir);
                    Logger?.LogInformation($"Cleaned up old test run: {Path.GetFileName(dir)}");
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning($"Failed to clean up old test run '{Path.GetFileName(dir)}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning($"Failed to enumerate test runs for cleanup: {ex.Message}");
        }
    }
}