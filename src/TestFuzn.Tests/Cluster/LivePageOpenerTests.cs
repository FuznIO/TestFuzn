using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Cluster.Remote;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class LivePageOpenerTests : Test
{
    private const string LiveUrl = "http://localhost:5170/testfuzn?clusterrun=abc12345";

    [Test]
    public async Task Verify_the_live_page_opens_only_outside_ci()
    {
        await Scenario()
            .Step("With no CI variable set, the live page opens", context =>
            {
                Assert.IsTrue(LivePageOpener.ShouldOpen(true, LiveUrl, new FakeEnvironment()));
            })
            .Step("Each CI variable stops it", context =>
            {
                foreach (var variable in new[] { "CI", "TF_BUILD", "GITHUB_ACTIONS", "GITLAB_CI", "JENKINS_URL" })
                    Assert.IsFalse(LivePageOpener.ShouldOpen(true, LiveUrl, new FakeEnvironment((variable, "true"))), variable);
            })
            .Step("CI=false is not a CI server", context =>
            {
                Assert.IsTrue(LivePageOpener.ShouldOpen(true, LiveUrl, new FakeEnvironment(("CI", "false"))));
            })
            .Step("OpenBrowser false stops it", context =>
            {
                Assert.IsFalse(LivePageOpener.ShouldOpen(false, LiveUrl, new FakeEnvironment()));
            })
            .Step("No link, or a link that is not http or https, opens nothing", context =>
            {
                Assert.IsFalse(LivePageOpener.ShouldOpen(true, null, new FakeEnvironment()));
                Assert.IsFalse(LivePageOpener.ShouldOpen(true, "not a url", new FakeEnvironment()));
                Assert.IsFalse(LivePageOpener.ShouldOpen(true, "file:///C:/Windows/notepad.exe", new FakeEnvironment()));
            });
    }

    private sealed class FakeEnvironment : IEnvironmentWrapper
    {
        private readonly Dictionary<string, string> _variables;

        public FakeEnvironment(params (string Name, string Value)[] variables)
        {
            _variables = variables.ToDictionary(v => v.Name, v => v.Value);
        }

        public string? GetEnvironmentVariable(string key)
        {
            return _variables.TryGetValue(key, out var value) ? value : null;
        }

        public string GetMachineName()
        {
            return "test-machine";
        }
    }
}
