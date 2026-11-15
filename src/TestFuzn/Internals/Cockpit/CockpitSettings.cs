namespace Fuzn.TestFuzn.Internals.Cockpit;

internal class CockpitSettings
{
    public const string UrlVariable = Cluster.Protocol.CockpitConnection.UrlVariable;
    public const string ApiKeyVariable = Cluster.Protocol.CockpitConnection.ApiKeyVariable;
    public const string SourceVariable = "TESTFUZN_COCKPIT_SOURCE";
    public const string SourceArgument = "cockpit-source";
    public const string SourceUsage = "--" + SourceArgument + " requires a value: --" + SourceArgument + "=<name>";
    public const string LogUrlVariable = "TESTFUZN_COCKPIT_LOG_URL";

    public string? Url { get; init; }
    public string? ApiKey { get; init; }
    public string? Source { get; init; }
    public string? Branch { get; init; }
    public string? Commit { get; init; }
    public string? BuildNumber { get; init; }
    public string? LogUrl { get; init; }

    public bool IsComplete => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Source);

    // The URL and API key come from TestFuzn:Cockpit in appsettings or the variables (a variable wins), the
    // same settings cluster runs use; nothing is sent without a source. Branch, commit and build number fall
    // back to what common CI systems set (GitHub Actions, Azure Pipelines, GitLab), branch and commit then to local git.
    // The source comes from --cockpit-source or the variable (the argument wins), never from appsettings: a source
    // in a checked-in file would put every developer's local run into the project's test runs.
    public static CockpitSettings Load(AppConfigurationManager? appConfiguration, IReadOnlyDictionary<string, string>? args = null)
    {
        CockpitSection? section = null;
        if (appConfiguration != null && appConfiguration.HasSection("Cockpit"))
            section = appConfiguration.GetRequiredSection<CockpitSection>("Cockpit");

        var url = Variable(UrlVariable);
        if (url == null && section != null && !string.IsNullOrWhiteSpace(section.Url))
            url = section.Url;
        if (url != null)
            url = url.TrimEnd('/');

        var apiKey = Variable(ApiKeyVariable);
        if (apiKey == null && section != null && !string.IsNullOrWhiteSpace(section.ApiKey))
            apiKey = section.ApiKey;

        var branch = FirstVariable("TESTFUZN_COCKPIT_BRANCH", "GITHUB_REF_NAME", "BUILD_SOURCEBRANCHNAME", "CI_COMMIT_REF_NAME");
        if (branch == null)
            branch = LocalGit.Branch;
        var commit = FirstVariable("TESTFUZN_COCKPIT_COMMIT", "GITHUB_SHA", "BUILD_SOURCEVERSION", "CI_COMMIT_SHA");
        if (commit == null)
            commit = LocalGit.Commit;

        return new CockpitSettings
        {
            Url = url,
            ApiKey = apiKey,
            Source = SourceFrom(args) ?? Variable(SourceVariable),
            Branch = branch,
            Commit = commit,
            BuildNumber = FirstVariable("TESTFUZN_COCKPIT_BUILD", "GITHUB_RUN_NUMBER", "BUILD_BUILDNUMBER", "CI_PIPELINE_ID"),
            LogUrl = Variable(LogUrlVariable) ?? CiBuildUrl()
        };
    }

    // The log is never uploaded (it can be gigabytes); without an explicit link, Cockpit links to the CI build page.
    private static string? CiBuildUrl()
    {
        var gitHubServer = Variable("GITHUB_SERVER_URL");
        var gitHubRepository = Variable("GITHUB_REPOSITORY");
        var gitHubRunId = Variable("GITHUB_RUN_ID");
        if (gitHubServer != null && gitHubRepository != null && gitHubRunId != null)
            return $"{gitHubServer.TrimEnd('/')}/{gitHubRepository}/actions/runs/{gitHubRunId}";

        var azureCollection = Variable("SYSTEM_COLLECTIONURI");
        var azureProject = Variable("SYSTEM_TEAMPROJECT");
        var azureBuildId = Variable("BUILD_BUILDID");
        if (azureCollection != null && azureProject != null && azureBuildId != null)
            return $"{azureCollection.TrimEnd('/')}/{Uri.EscapeDataString(azureProject)}/_build/results?buildId={azureBuildId}";

        return Variable("CI_JOB_URL");
    }

    private static string? SourceFrom(IReadOnlyDictionary<string, string>? args)
    {
        if (args == null || !args.TryGetValue(SourceArgument, out var value))
            return null;
        // A bare --cockpit-source, or one with a space before the name, parses as a flag.
        if (value == ArgumentsParser.FlagValue || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(SourceUsage);

        return value.Trim();
    }

    private static string? FirstVariable(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Variable(name);
            if (value != null)
                return value;
        }

        return null;
    }

    private static string? Variable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private class CockpitSection
    {
        public string? Url { get; set; }
        public string? ApiKey { get; set; }
    }
}
