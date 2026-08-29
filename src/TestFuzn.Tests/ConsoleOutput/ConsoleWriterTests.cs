using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Execution;
using Fuzn.TestFuzn.Internals.Execution.Producers.Simulations;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Tests.StandaloneRunner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.ConsoleOutput;

/// <summary>
/// Pins <see cref="ConsoleWriter"/> over a real <see cref="TestExecutionState"/>: on the
/// standalone path (a framework with real-time output) the load summary is handed to the
/// adapter from the collectors' results — for a stopped run without a reason, Ctrl+C's and the
/// quit key's, exactly as for a completed one, with nothing else asked of the terminal — and the
/// standard test's result table is rendered through the real standalone adapter as an exact
/// golden; on the MSTest path (no real-time output) the status line names the stop's reason
/// when there is one and reads "Stopped" alone when there is none, instead of failing on it.
/// </summary>
[TestClass]
public class ConsoleWriterTests : Test
{
    private const string StepName = "Checkout step";

    private static DateTime At(double seconds)
    {
        return SyntheticLoadSnapshots.At(seconds);
    }

    private static TestSession Session()
    {
        var testSession = new TestSession("console-writer-tests");
        testSession.TestsResultsDirectory = Path.Combine(Path.GetTempPath(), "TestFuznResults");
        testSession.TestRunId = "console-writer-tests-run";
        return testSession;
    }

    /// <summary>A load scenario with one step and a fixed load; the state initialized over the adapter, its run marked 0–5 s.</summary>
    private static TestExecutionState LoadState(Contracts.Adapters.ITestFrameworkAdapter adapter)
    {
        var scenario = new Scenario("Checkout flow");
        scenario.Id = "checkout-flow";
        scenario.Steps.Add(new Step { Name = StepName, Id = "checkout-step" });
        scenario.SimulationsAction = (scenarioContext, simulations) => Task.CompletedTask;
        scenario.SimulationsInternal.Add(new FixedLoadConfiguration(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)));

        var state = new TestExecutionState(Session());
        state.Init(adapter, new FakeTest(), scenario);
        state.TestResult.MarkPhaseAsStarted(StandardTestPhase.Init, At(0));
        state.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Cleanup, At(5));

        var collector = state.LoadCollectors[scenario.Name];
        collector.MarkPhaseAsStarted(LoadTestPhase.Init, At(0));
        collector.MarkPhaseAsCompleted(LoadTestPhase.Init, At(1));
        collector.MarkPhaseAsStarted(LoadTestPhase.Measurement, At(1));
        collector.MarkPhaseAsCompleted(LoadTestPhase.Measurement, At(4));
        collector.MarkPhaseAsStarted(LoadTestPhase.Cleanup, At(4));
        collector.MarkPhaseAsCompleted(LoadTestPhase.Cleanup, At(5));
        return state;
    }

    private static List<string> MarkupEvents(List<string> events)
    {
        return events.Where(eventName => eventName.StartsWith(FakeTestFrameworkAdapter.MarkupEventPrefix, StringComparison.Ordinal)).Select(eventName => eventName.Substring(FakeTestFrameworkAdapter.MarkupEventPrefix.Length)).ToList();
    }

    [Test]
    public async Task Verify_a_stopped_run_without_a_reason_prints_Stopped_on_the_MSTest_path()
    {
        await Scenario()
            .Step("Stopped by Ctrl+C or the quit key — no reason — the status line reads Stopped alone", context =>
            {
                var events = new List<string>();
                var adapter = new FakeTestFrameworkAdapter(events) { SupportsRealTimeConsoleOutput = false };
                var state = LoadState(adapter);
                state.ExecutionStatus = ExecutionStatus.Stopped;

                new ConsoleWriter().WriteSummary(state);

                var markup = MarkupEvents(events);
                Assert.AreEqual("[bold]Total elapsed Time:[/] [yellow]00:00:05:00[/]", markup[0]);
                Assert.AreEqual("[red]Status: Stopped[/]\r\n", markup[1]);
                Assert.Contains("advanced-table", events);
            })
            .Step("Stopped by an assert — a reason — the status line names it, as before", context =>
            {
                var events = new List<string>();
                var adapter = new FakeTestFrameworkAdapter(events) { SupportsRealTimeConsoleOutput = false };
                var state = LoadState(adapter);
                state.ExecutionStatus = ExecutionStatus.Stopped;
                state.ExecutionStoppedReason = new InvalidOperationException("Assert while running failed");

                new ConsoleWriter().WriteSummary(state);

                Assert.AreEqual("[red]Status: Stopped, reason: Assert while running failed[/]\r\n", MarkupEvents(events)[1]);
            })
            .Step("A completed run reads Completed successfully", context =>
            {
                var events = new List<string>();
                var adapter = new FakeTestFrameworkAdapter(events) { SupportsRealTimeConsoleOutput = false };
                var state = LoadState(adapter);
                state.MarkScenarioProducersCompleted("Checkout flow");
                state.MarkConsumingCompleted();

                new ConsoleWriter().WriteSummary(state);

                Assert.AreEqual("[green]Status: Completed successfully.[/]\r\n", MarkupEvents(events)[1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_standalone_path_hands_the_collectors_results_to_the_adapter_summary()
    {
        await Scenario()
            .Step("A stopped run without a reason gets the same summary as a completed one — the collectors' results, rendered by the adapter, nothing else written", context =>
            {
                var host = new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null));
                using var adapter = new FakeStandaloneRunnerAdapter(host);
                var state = LoadState(adapter);
                state.ExecutionStatus = ExecutionStatus.Stopped;

                new ConsoleWriter().WriteSummary(state);

                var results = new Dictionary<Scenario, Contracts.Results.Load.ScenarioLoadResult>();
                results.Add(state.Scenarios[0], state.LoadCollectors["Checkout flow"].GetCurrentResult());
                var expected = LoadSummaryLayout.Render(results, BaseStandaloneRunnerAdapterWidth(), ColorMode.None).Select(line => line.Text + Environment.NewLine).ToList();
                CollectionAssert.AreEqual(expected, host.Writer.Writes.ToList());
                Assert.IsGreaterThan(10, host.Writer.Writes.Count);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
            })
            .Run();
    }

    private static int BaseStandaloneRunnerAdapterWidth()
    {
        return Fuzn.TestFuzn.StandaloneRunner.BaseStandaloneRunnerAdapter.DefaultWidth;
    }

    [Test]
    public async Task Verify_standard_test_summary_golden_through_the_standalone_adapter()
    {
        await Scenario()
            .Step("A standard test with two steps, a comment, a failed step and a sub-step renders its result table and the report link as exact plain lines", context =>
            {
                var host = new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null));
                using var adapter = new FakeStandaloneRunnerAdapter(host);

                var scenario = new Scenario("Checkout");
                scenario.Id = "checkout";
                scenario.Steps.Add(new Step { Name = "Open page", Id = "open-page" });
                scenario.Steps.Add(new Step { Name = "Check title", Id = "check-title" });

                var session = Session();
                var state = new TestExecutionState(session);
                state.Init(adapter, new FakeTest(), scenario);
                state.TestResult.MarkPhaseAsStarted(StandardTestPhase.Init, At(0));
                state.TestResult.MarkPhaseAsCompleted(StandardTestPhase.Cleanup, At(1));

                var openPage = new StepStandardResult { Name = "Open page", Id = "open-page", Status = StepStatus.Passed, Duration = TimeSpan.FromMilliseconds(120) };
                openPage.Comments = new List<Comment> { new Comment { Text = "Page loaded" } };
                var readHeader = new StepStandardResult { Name = "Read header", Id = "read-header", Status = StepStatus.Passed, Duration = TimeSpan.FromMilliseconds(2) };
                var checkTitle = new StepStandardResult { Name = "Check title", Id = "check-title", Status = StepStatus.Failed, Duration = TimeSpan.FromMilliseconds(5) };
                checkTitle.StepResults = new List<StepStandardResult> { readHeader };

                var iteration = new IterationResult();
                iteration.CorrelationId = "c6c08e49-9ec0-4da8-bbc0-35745e462ebe";
                iteration.StepResults.Add(openPage.Name, openPage);
                iteration.StepResults.Add(checkTitle.Name, checkTitle);
                state.TestResult.IterationResults.Add(iteration);

                new ConsoleWriter().WriteSummary(state);

                // Column widths: CorrelationId (13) sets the first column, the correlation id
                // spanning the next four sets 9 each, the key/value cells (13) the last — plus
                // four each: 17, 13, 13, 13, 17, with six borders: 79 columns.
                var reportPath = Path.GetFullPath(Path.Combine(session.TestsResultsDirectory, "TestReport.html"));
                var expected = new[]
                {
                    Environment.NewLine + Environment.NewLine,
                    "╭" + new string('─', 77) + "╮" + Environment.NewLine,
                    "│ Scenario: Checkout" + new string(' ', 40) + "│ Duration        │" + Environment.NewLine,
                    "│" + new string(' ', 59) + "│ 00:00:01:00     │" + Environment.NewLine,
                    "│ Status: Failed" + new string(' ', 62) + "│" + Environment.NewLine,
                    "├" + new string('─', 77) + "┤" + Environment.NewLine,
                    "│ Step Name" + new string(' ', 67) + "│" + Environment.NewLine,
                    "├" + new string('─', 77) + "┤" + Environment.NewLine,
                    "│ CorrelationId   │ c6c08e49-9ec0-4da8-bbc0-35745e462ebe" + new string(' ', 22) + "│" + Environment.NewLine,
                    "│ Step 1: Open page" + new string(' ', 41) + "│ Passed   120 ms │" + Environment.NewLine,
                    "│   // Page loaded" + new string(' ', 60) + "│" + Environment.NewLine,
                    "│ Step 2: Check title" + new string(' ', 39) + "│ Failed     5 ms │" + Environment.NewLine,
                    "│ ↳ Step 2.1: Read header" + new string(' ', 35) + "│ Passed     2 ms │" + Environment.NewLine,
                    "╰" + new string('─', 77) + "╯" + Environment.NewLine,
                    Environment.NewLine + Environment.NewLine,
                    "Report: " + reportPath + Environment.NewLine,
                    Environment.NewLine + Environment.NewLine
                };
                CollectionAssert.AreEqual(expected, host.Writer.Writes.ToList());
                foreach (var write in host.Writer.Writes)
                    Assert.DoesNotContain(AnsiCodes.Escape, write);
            })
            .Run();
    }
}
