using Fuzn.TestFuzn.Internals.Cluster.Protocol;
using Fuzn.TestFuzn.Internals.Cockpit;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// TestFuzn:Cluster in appsettings: normal file rules, so a pool or worker count can differ per environment.
internal class ClusterOptions
{
    public const string SectionName = "Cluster";

    public string Pool { get; set; } = "default";
    public int Workers { get; set; } = 1;
    public TimeSpan ConnectionTimeout { get; set; } = ClusterProtocol.DefaultConnectionTimeout;
    public string? PackagePath { get; set; }
    public bool OpenBrowser { get; set; } = true;

    public static ClusterOptions Load(TestSession session)
    {
        var appConfiguration = session.Configuration.AppConfiguration;
        var options = appConfiguration.HasSection(SectionName)
            ? appConfiguration.GetRequiredSection<ClusterOptions>(SectionName)
            : new ClusterOptions();

        if (string.IsNullOrWhiteSpace(options.Pool))
            throw new InvalidOperationException("TestFuzn:Cluster:Pool cannot be empty.");
        if (options.Workers < 1)
            throw new InvalidOperationException($"TestFuzn:Cluster:Workers must be at least 1, it is {options.Workers}.");
        if (options.ConnectionTimeout <= TimeSpan.Zero)
            options.ConnectionTimeout = ClusterProtocol.DefaultConnectionTimeout;

        return options;
    }
}

// Where the remote client reaches Cockpit: the same URL and API key as the Cockpit sink (TestFuzn:Cockpit:Url
// and ApiKey in appsettings, or TESTFUZN_COCKPIT_URL and TESTFUZN_COCKPIT_API_KEY; a variable wins). The key
// is a project API key from Cockpit; the run belongs to that project. A key in the file is fine for a local
// Cockpit; a real one stays in the variable.
internal static class CockpitClientSettings
{
    private const string Hint = "Check TestFuzn:Cockpit:Url and TestFuzn:Cockpit:ApiKey, or TESTFUZN_COCKPIT_URL and TESTFUZN_COCKPIT_API_KEY.";

    public static CockpitConnection Connect(TestSession session)
    {
        var settings = CockpitSettings.Load(session.Configuration.AppConfiguration);
        if (settings.Url == null || settings.ApiKey == null)
            throw new InvalidOperationException("Cluster runs go through Cockpit, and this run has no Cockpit address or API key. Set TestFuzn:Cockpit:Url and TestFuzn:Cockpit:ApiKey in appsettings, or TESTFUZN_COCKPIT_URL and TESTFUZN_COCKPIT_API_KEY. The API key is a project key from the project's settings in Cockpit.");
        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out _))
            throw new InvalidOperationException($"The Cockpit address '{settings.Url}' is not a URL. {Hint}");

        return new CockpitConnection(settings.Url, CockpitConnection.ApiKeyHeader, settings.ApiKey.Trim(), Hint);
    }
}
