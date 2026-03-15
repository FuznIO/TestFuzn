using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn;

/// <summary>
/// Provides global state and configuration for the TestFuzn framework.
/// </summary>
public static class GlobalState
{
    internal static string TestsOutputDirectory
    {
        get => TestSession.Current.TestsOutputDirectory;
        set => TestSession.Current.TestsOutputDirectory = value;
    }

    internal static TestFuznConfiguration Configuration
    {
        get => TestSession.Current.Configuration;
        set => TestSession.Current.Configuration = value;
    }

    internal static IServiceProvider ServiceProvider
    {
        get => TestSession.Current.ServiceProvider;
        set => TestSession.Current.ServiceProvider = value;
    }

    internal static ILogger Logger
    {
        get => TestSession.Current.Logger;
        set => TestSession.Current.Logger = value;
    }

    internal static string TestRunId
    {
        get => TestSession.Current.TestRunId;
        set => TestSession.Current.TestRunId = value;
    }

    internal static string NodeName
    {
        get => TestSession.Current.NodeName;
        set => TestSession.Current.NodeName = value;
    }

    internal static void EnsureInitialized(ITestFrameworkAdapter testFramework)
    {
        TestSession.Current.EnsureInitialized(testFramework);
    }
}