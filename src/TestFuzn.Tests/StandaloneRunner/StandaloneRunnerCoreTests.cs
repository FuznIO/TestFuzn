using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.StandaloneRunner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.StandaloneRunner;

/// <summary>
/// Pins the runner core's handling of a <c>--test-name</c> given without a value, which the
/// argument parser records as a bare flag: an invocation error with the usage message, written
/// through the framework adapter, exit code 1 — never a lookup of a test named "true", never a
/// silent fall-through to the selection menu (which would exit 0 on the redirected input here).
/// And the selection menu shown when no test is named: it runs over the host the core was
/// given and the adapter the run would use — whose cancellation, the Ctrl+C path, ends it — and
/// a quit, on either the redirected fallback or the full-screen menu, exits 0 with nothing run.
/// </summary>
[TestClass]
public class StandaloneRunnerCoreTests : Test
{
    private static readonly string UsageEvent = FakeTestFrameworkAdapter.MarkupEventPrefix + "[red]" + StandaloneRunnerCore.TestNameUsage + "[/]";

    private const string EnterSequence = AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap;
    private const string RestoreSequence = AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen;

    [Test]
    public async Task Verify_a_test_name_without_a_value_is_an_invocation_error()
    {
        await Scenario()
            .Step("A bare --test-name exits 1 with the usage message written through the adapter, and nothing else", async context =>
            {
                var events = new List<string>();
                var exitCode = await new StandaloneRunnerCore().Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                var usage = Assert.ContainsSingle(events);
                Assert.AreEqual(UsageEvent, usage);
            })
            .Step("--test-name with a space instead of = is the same error, whatever follows", async context =>
            {
                var events = new List<string>();
                var exitCode = await new StandaloneRunnerCore().Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name", "Fuzn.TestFuzn.Tests.SomeTests.Some_test" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                var usage = Assert.ContainsSingle(events);
                Assert.AreEqual(UsageEvent, usage);
            })
            .Step("A test name that names no test is still reported as not found with exit 1, without an adapter", async context =>
            {
                var events = new List<string>();
                var exitCode = await new StandaloneRunnerCore().Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name=Fuzn.TestFuzn.Tests.SomeTests.Does_not_exist" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.IsEmpty(events);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_selection_menu_runs_over_the_host_and_a_quit_exits_0()
    {
        await Scenario()
            .Step("On a redirected terminal the prompt fallback lists the discovered tests and the end of the input quits with exit 0, nothing written through the adapter", async context =>
            {
                var host = new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null));
                var events = new List<string>();

                var exitCode = await new StandaloneRunnerCore(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(0, exitCode);
                Assert.IsEmpty(events);
                Assert.AreEqual(TestSelectionMenu.Title + Environment.NewLine, host.Writer.Writes[0]);
                Assert.AreEqual(TestSelectionMenu.Prompt + Environment.NewLine, host.Writer.Writes[host.Writer.Writes.Count - 1]);
                Assert.Contains(write => write.Contains(typeof(StandaloneRunnerCoreTests).FullName + "." + nameof(Verify_the_selection_menu_runs_over_the_host_and_a_quit_exits_0), StringComparison.Ordinal), host.Writer.Writes);
                Assert.AreEqual(1, host.Reader.ReadLineCallCount);
                Assert.AreEqual(0, host.Reader.TryReadKeyCallCount);
            })
            .Step("On a terminal with live view support Escape leaves the full-screen menu with the terminal restored and exit 0", async context =>
            {
                var host = new FakeLiveViewHost(new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: ColorMode.None));
                host.Writer.WindowWidth = 120;
                host.Writer.WindowHeight = 40;
                host.OnDelay = tick => host.Reader.Press(ConsoleKey.Escape);
                var events = new List<string>();

                var exitCode = await new StandaloneRunnerCore(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(0, exitCode);
                Assert.IsEmpty(events);
                Assert.AreEqual(EnterSequence, host.Writer.Writes[0]);
                Assert.AreEqual(RestoreSequence, host.Writer.Writes[host.Writer.Writes.Count - 1]);
                Assert.AreEqual(1, host.Writer.Writes.Count(write => write == RestoreSequence));
            })
            .Step("The adapter's cancellation — what Ctrl+C does — ends the menu with the terminal restored and exit 0", async context =>
            {
                var host = new FakeLiveViewHost(new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: ColorMode.None));
                host.Writer.WindowWidth = 120;
                host.Writer.WindowHeight = 40;
                var events = new List<string>();
                var adapter = new FakeTestFrameworkAdapter(events);
                host.OnDelay = tick => adapter.Cancel();

                var exitCode = await new StandaloneRunnerCore(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run" }, () => adapter);

                Assert.AreEqual(0, exitCode);
                Assert.IsEmpty(events);
                Assert.AreEqual(1, host.DelayCount);
                Assert.AreEqual(EnterSequence, host.Writer.Writes[0]);
                Assert.AreEqual(RestoreSequence, host.Writer.Writes[host.Writer.Writes.Count - 1]);
            })
            .Step("The adapter's cancellation while the prompt fallback waits for a line — Ctrl+C at the prompt — ends the run with exit 0 at once", async context =>
            {
                var host = new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: false, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: null, noColor: null));
                var gate = new ManualResetEventSlim(false);
                host.Reader.ReadLineGate = gate;
                var events = new List<string>();
                var adapter = new FakeTestFrameworkAdapter(events);

                var run = new StandaloneRunnerCore(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run" }, () => adapter);

                Assert.IsTrue(SpinWait.SpinUntil(() => host.Reader.ReadLineCallCount == 1, TimeSpan.FromSeconds(30)));
                Assert.IsFalse(run.IsCompleted);

                adapter.Cancel();

                Assert.AreEqual(0, await run.WaitAsync(TimeSpan.FromSeconds(30)));
                Assert.IsEmpty(events);
                Assert.AreEqual(TestSelectionMenu.Prompt + Environment.NewLine, host.Writer.Writes[host.Writer.Writes.Count - 1]);
                gate.Set();
            })
            .Run();
    }
}
