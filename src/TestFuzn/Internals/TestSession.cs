using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Sinks;
using Fuzn.TestFuzn.Internals.Cleanup;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Consumers;
using Fuzn.TestFuzn.Internals.Execution.Producers;
using Fuzn.TestFuzn.Internals.Init;
using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.Logger;
using Fuzn.TestFuzn.Internals.Reports.EmbeddedResources;
using Fuzn.TestFuzn.Internals.Reports.Load;
using Fuzn.TestFuzn.Internals.Reports.Standard;
using Fuzn.TestFuzn.Internals.Results.Standard;
using Fuzn.TestFuzn.Internals.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;

namespace Fuzn.TestFuzn.Internals;

internal class TestSession
{
    private static readonly AsyncLocal<TestSession> _current = new();
    private static TestSession? _default;

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
    internal string Id { get; }

    private IConfigurationRoot _configRoot;
    private readonly object _configLocker = new();

    //internal IConfigurationRoot ConfigRoot
    //{
    //    get => _configRoot ??= BuildConfigRoot();
    //    set => _configRoot = value;
    //}

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

        await Init<TStartup>(
            environmentWrapper,
            new FileSystem(),
            new ArgumentsParser(environmentWrapper),
            testFramework, 
            args);
    }

    internal async Task Init<TStartup>(
        IEnvironmentWrapper environmentWrapper,
        IFileSystem fileSystem,
        ArgumentsParser argumentParser,
        ITestFrameworkAdapter testFramework,
        Dictionary<string, string>? args = null)
        where TStartup : IStartup, new()
    {
        StartupInstance = new TStartup();
        TestRunStartTime = DateTime.UtcNow;
        TestRunId = $"{DateTime.Now:yyyy-MM-dd_HH-mm}__{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        TargetEnvironment = argumentParser.GetValueFromArgsOrEnvironmentVariable(
                                        args, "target-environment", "TESTFUZN_TARGET_ENVIRONMENT");
        ExecutionEnvironment = argumentParser.GetValueFromArgsOrEnvironmentVariable(
                                        args, "execution-environment", "TESTFUZN_EXECUTION_ENVIRONMENT");
        
        var tagsInclude = argumentParser.GetValueFromArgsOrEnvironmentVariable(
                                args, "tags-filter-include", "TESTFUZN_TAGS_FILTER_INCLUDE");
        if (!string.IsNullOrEmpty(tagsInclude))
        {
            TagsFilterInclude.AddRange(tagsInclude.Split(',').Select(t => t.Trim()));
        }

        var tagsExclude = argumentParser.GetValueFromArgsOrEnvironmentVariable(args, "tags-filter-exclude", "TESTFUZN_TAGS_FILTER_EXCLUDE");
        if (!string.IsNullOrEmpty(tagsExclude))
        {
            TagsFilterExclude.AddRange(tagsExclude.Split(',').Select(t => t.Trim()));
        }

        NodeName = environmentWrapper.GetMachineName();

        var testAssemblyName = StartupInstance.GetType().Assembly.GetName().Name;
        TestsOutputDirectory = Path.Combine(testFramework.TestResultsDirectory, "TestFuznResults", testAssemblyName, $"{TestRunId}");
        if (Id != "default")
            TestsOutputDirectory = TestsOutputDirectory + "_" + Id;

        fileSystem.CreateDirectory(TestsOutputDirectory);

        Logger = Internals.Logging.LoggerFactory.CreateLogger(fileSystem, TestsOutputDirectory);
        Logger.LogInformation("Logging initialized");

        var services = new ServiceCollection();
        AddServices(services, fileSystem, Logger);

        var configuration = new TestFuznConfiguration(services);
        configuration.Suite = new SuiteInfo();
        configuration.Suite.Name = testAssemblyName;
        StartupInstance.Configure(configuration);            
        Configuration = configuration;

        // Build the service provider after all plugins have registered their services
        ServiceProvider = configuration.BuildServiceProvider();

        if (StartupInstance is IBeforeSuite initGlobalInstance)
        {
            var context = ContextFactory.CreateContext(ServiceProvider, testFramework, "Init");
            await initGlobalInstance.BeforeSuite(context);
        }

        foreach (var plugin in ServiceProvider.GetServices<ISinkPlugin>())
            await plugin.InitSuite();

        foreach (var plugin in Configuration.ContextPlugins)
            await plugin.InitSuite();

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(fileSystem, "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Styles.testfuzn.css",
                                        Path.Combine(TestsOutputDirectory, "assets/styles/testfuzn.css"));

        await EmbeddedResourceHelper.WriteEmbeddedResourceToFile(fileSystem, "Fuzn.TestFuzn.Internals.Reports.EmbeddedResources.Scripts.chart.js",
                                        Path.Combine(TestsOutputDirectory, "assets/scripts/chart.js"));

        IsInitializeGlobalExecuted = true;
    }

    private void AddServices(ServiceCollection services,
        IFileSystem fileSystem,
        ILogger logger)
    {
        services.AddSingleton(this);
        services.AddSingleton<IFileSystem>(fileSystem);
        services.AddSingleton<ILogger>(logger);
        services.AddSingleton(new FileManager(fileSystem));
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
            await cleanupGlobalInstance.AfterSuite(ContextFactory.CreateContext(ServiceProvider, testFramework, "Cleanup"));
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
    }

    //private IConfigurationRoot BuildConfigRoot()
    //{
    //    lock (_configLocker)
    //    {
    //        if (_configRoot != null)
    //            return _configRoot;

    //        var builder = new ConfigurationBuilder()
    //                            .SetBasePath(Directory.GetCurrentDirectory())
    //                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

    //        var executionEnv = ExecutionEnvironment;
    //        var targetEnv = TargetEnvironment;
    //        var nodeName = NodeName;

    //        if (!string.IsNullOrEmpty(executionEnv))
    //            builder.AddJsonFile($"appsettings.exec-{executionEnv}.json", optional: true, reloadOnChange: false);

    //        if (!string.IsNullOrEmpty(targetEnv))
    //            builder.AddJsonFile($"appsettings.target-{targetEnv}.json", optional: true, reloadOnChange: false);

    //        if (!string.IsNullOrEmpty(executionEnv) && !string.IsNullOrEmpty(targetEnv))
    //            builder.AddJsonFile($"appsettings.exec-{executionEnv}.target-{targetEnv}.json", optional: true, reloadOnChange: false);

    //        if (!string.IsNullOrEmpty(nodeName))
    //            builder.AddJsonFile($"appsettings.{nodeName}.json", optional: true, reloadOnChange: false);

    //        _configRoot = builder.Build();

    //        return _configRoot;
    //    }
    //}
}
