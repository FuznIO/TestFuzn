using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Fuzn.TestFuzn.Tests.Cluster;

// End to end: Cockpit (started from the Cockpit repository with start-service.ps1) and the three Docker
// workers from Start-Dev.ps1 in the repo root. Each test runs this test exe as a remote client, the way a
// developer or a pipeline would.
[TestClass]
[Tags("integration")]
public class ClusterRunTests : Test
{
    private const string TestClass = "Fuzn.TestFuzn.Tests.Cluster.ClusterLoadTests.";
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    [Test]
    public async Task Verify_a_remote_run_spreads_the_load_and_prints_the_result()
    {
        RequireCockpit();

        await Scenario()
            .Step("The test passes on three workers with the same total load as on one machine, and the client prints the live link", async context =>
            {
                var run = await RunClient("--cluster", "--test-name=" + TestClass + "Fixed_load_with_warmup");
                Assert.AreEqual(0, run.ExitCode, run.Output);
                Assert.Contains("Total Iterations 30", run.Output);
                Assert.Contains("Live: http", run.Output);
                Assert.Contains("/testfuzn?clusterrun=", run.Output);
            })
            .Step("Once data reaches both workers and feeds their input data", async context =>
            {
                var run = await RunClient("--cluster", "--test-name=" + TestClass + "Once_data_feeds_the_input_data");
                Assert.AreEqual(0, run.ExitCode, run.Output);
            });
    }

    [Test]
    public async Task Verify_a_run_without_a_source_is_refused()
    {
        await Scenario()
            .Step("The client stops before it sends anything to Cockpit and says how to set the source", async context =>
            {
                var run = await RunClient(new Dictionary<string, string> { ["TESTFUZN_COCKPIT_SOURCE"] = "" },
                    "--cluster", "--test-name=" + TestClass + "Fixed_load_with_warmup");
                Assert.AreNotEqual(0, run.ExitCode, run.Output);
                Assert.Contains("A cluster run needs a source", run.Output);
                Assert.Contains("TESTFUZN_COCKPIT_SOURCE", run.Output);
            });
    }

    [Test]
    public async Task Verify_an_assert_that_fails_on_the_controller_fails_the_run()
    {
        RequireCockpit();

        await Scenario()
            .Step("AssertWhileRunning runs on the merged stats and stops the run", async context =>
            {
                var run = await RunClient("--cluster", "--test-name=" + TestClass + "ShouldFail_in_cluster_assert_while_running");
                Assert.AreEqual(1, run.ExitCode, run.Output);
                Assert.Contains("Too many iterations for this test.", run.Output);
            });
    }

    [Test]
    public async Task Verify_a_run_waits_in_the_queue_while_the_workers_are_busy()
    {
        RequireCockpit();

        await Scenario()
            .Step("A second run that needs the same workers waits in the queue, then runs and passes", async context =>
            {
                var first = RunClient("--cluster", "--run-name=e2e queue first", "--test-name=" + TestClass + "Steady_load");
                await Task.Delay(TimeSpan.FromSeconds(8));
                var second = await RunClient("--cluster", "--run-name=e2e queue second", "--test-name=" + TestClass + "Fixed_load_with_warmup");
                var firstRun = await first;

                Assert.AreEqual(0, firstRun.ExitCode, firstRun.Output);
                Assert.AreEqual(0, second.ExitCode, second.Output);
                Assert.Contains("Queued: next in line", second.Output);
                Assert.Contains("Total Iterations 30", second.Output);
            });
    }

    [Test]
    public async Task Verify_cluster_mode_runs_one_load_test_per_session()
    {
        RequireCockpit();

        await Scenario()
            .Step("With two load tests selected, the first runs on the cluster and the second fails without running", async context =>
            {
                var filter = "FullyQualifiedName~" + TestClass + "Fixed_load_with_warmup|FullyQualifiedName~" + TestClass + "Gradual_load_adds_up_over_the_workers";
                var run = await RunClient(new Dictionary<string, string> { ["TESTFUZN_CLUSTER"] = "true" }, "--runner=mstest", "--filter", filter);
                Assert.AreNotEqual(0, run.ExitCode, run.Output);
                Assert.Contains("Cluster mode runs one load test per session.", run.Output);
            });
    }

    private static Task<(int ExitCode, string Output)> RunClient(params string[] arguments)
        => RunClient(new Dictionary<string, string>(), arguments);

    private static async Task<(int ExitCode, string Output)> RunClient(Dictionary<string, string> environment, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Fuzn.TestFuzn.Tests.Cluster.dll"));
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        // An automated run: no live page in the browser for each client this test starts.
        startInfo.Environment["CI"] = "true";
        startInfo.Environment["TESTFUZN_COCKPIT_SOURCE"] = "e2e-cluster";
        foreach (var variable in environment)
            startInfo.Environment[variable.Key] = variable.Value;

        var output = new StringBuilder();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(RunTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"'{string.Join(" ", arguments)}' did not end within {RunTimeout}.{Environment.NewLine}{output}");
        }

        lock (output)
            return (process.ExitCode, output.ToString());
    }

    private static void RequireCockpit()
    {
        var url = CockpitUrl();
        if (!IsClusterOn(url))
            Assert.Inconclusive($"Cockpit with the TestFuzn integration is not running at {url}. Start it with start-service.ps1 in the Cockpit repository, and the workers with Start-Dev.ps1 in the repo root.");
    }

    private static string CockpitUrl()
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        return settings.RootElement.GetProperty("TestFuzn").GetProperty("Cockpit").GetProperty("Url").GetString()!.TrimEnd('/');
    }

    private static bool IsClusterOn(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var json = http.GetStringAsync(url + "/api/testfuzn/cluster/status").GetAwaiter().GetResult();
            using var status = JsonDocument.Parse(json);
            return status.RootElement.GetProperty("enabled").GetBoolean();
        }
        catch (Exception)
        {
            return false;
        }
    }
}
