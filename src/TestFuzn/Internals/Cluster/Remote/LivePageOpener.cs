using System.Diagnostics;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// Opens a cluster run's live page in the browser, so a run started from an IDE shows its charts at once:
// Test Explorer shows the test output only when the test ends. Never on a CI server.
internal static class LivePageOpener
{
    private static readonly string[] CiVariables =
    [
        "CI", "TF_BUILD", "GITHUB_ACTIONS", "GITLAB_CI", "JENKINS_URL", "TEAMCITY_VERSION",
        "BUILDKITE", "CIRCLECI", "APPVEYOR", "BITBUCKET_BUILD_NUMBER"
    ];

    public static bool IsCi(IEnvironmentWrapper environment)
    {
        if (environment == null)
            throw new ArgumentNullException(nameof(environment), "Environment cannot be null.");

        foreach (var variable in CiVariables)
        {
            var value = environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool ShouldOpen(bool openBrowser, string? liveUrl, IEnvironmentWrapper environment)
    {
        if (!openBrowser || liveUrl == null)
            return false;
        if (!Uri.TryCreate(liveUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        return !IsCi(environment);
    }

    // A machine without a browser (a container, a server) is fine: the link is in the output too.
    // Returns null when the browser started, else why it did not.
    public static string? TryOpen(string url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
