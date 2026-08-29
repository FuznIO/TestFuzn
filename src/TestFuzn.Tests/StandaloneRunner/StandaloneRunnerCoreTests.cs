using Fuzn.TestFuzn.Internals;
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
/// And the startup banner: written through the same host once the test is resolved, on the
/// direct-run path as the first terminal output and on the menu-pick path right after the
/// menu has restored the terminal (or after the prompt fallback's lines), never for a name
/// that names no test. The banner runs here name a class that is not a test class, so the run
/// fails right after the banner with exit 1 — before the process-wide default session, which
/// the suite's own tests resolve in parallel, is touched.
/// </summary>
[TestClass]
public class StandaloneRunnerCoreTests : Test
{
    private static readonly string UsageEvent = FakeTestFrameworkAdapter.MarkupEventPrefix + "[red]" + StandaloneRunnerCore.TestNameUsage + "[/]";

    private const string EnterSequence = AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap;
    private const string RestoreSequence = AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen;

    private const string FakeTestName = "Fuzn.Shop.Tests.CheckoutTests.Verify_checkout";

    // The plain banner the fake test gets: the detail line's target environment is whatever the
    // process environment holds (empty under the suite's run settings), so only its start is pinned.
    private static readonly string BannerLogoWrite = "⚡ TestFuzn" + Environment.NewLine;
    private static readonly string BannerTitleWrite = StandaloneTestRunner.RunningTestLabel + " " + FakeTestName + Environment.NewLine;
    private const string BannerDetailStart = "Assembly: Fuzn.TestFuzn.Tests · Target environment: ";

    private static FakeLiveViewHost LiveHost()
    {
        var host = new FakeLiveViewHost(new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: ColorMode.None));
        host.Writer.WindowWidth = 120;
        host.Writer.WindowHeight = 40;
        return host;
    }

    private static FakeLiveViewHost RedirectedHost()
    {
        return new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: "truecolor", noColor: null));
    }

    private static StandaloneRunnerCore CoreOverFakeTest(FakeLiveViewHost host)
    {
        return new StandaloneRunnerCore(host, new FakeDiscoverTests(FakeTestName));
    }

    /// <summary>The three banner writes are the last writes, plain, with nothing after them.</summary>
    private static void AssertBannerIsLastWrites(FakeTerminalWriter writer)
    {
        Assert.IsGreaterThanOrEqualTo(StartupBanner.Height, writer.Writes.Count);
        var bannerStart = writer.Writes.Count - StartupBanner.Height;
        Assert.AreEqual(BannerLogoWrite, writer.Writes[bannerStart]);
        Assert.AreEqual(BannerTitleWrite, writer.Writes[bannerStart + 1]);
        Assert.StartsWith(BannerDetailStart, writer.Writes[bannerStart + 2]);
        Assert.EndsWith(Environment.NewLine, writer.Writes[bannerStart + 2]);
    }

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
            .Step("Not found writes no banner: nothing goes through the host, whose terminal is never even asked for", async context =>
            {
                var host = LiveHost();
                var events = new List<string>();
                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name=Does.Not.Exist" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.IsEmpty(events);
                Assert.IsEmpty(host.Writer.Writes);
                Assert.AreEqual(0, host.DetectCapabilitiesCallCount);
                Assert.AreEqual(0, host.CreateTerminalWriterCallCount);
                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_startup_banner_is_written_once_the_test_is_resolved_on_both_paths()
    {
        await Scenario()
            .Step("Direct run: the banner is the only terminal output before the run starts — no alternate screen, no reader, no size read — and the run then fails on its test class with exit 1", async context =>
            {
                var host = LiveHost();
                var events = new List<string>();
                var defaultSessionBefore = TestSession.Default;

                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name=" + FakeTestName }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.HasCount(StartupBanner.Height, host.Writer.Writes);
                AssertBannerIsLastWrites(host.Writer);
                Assert.DoesNotContain(EnterSequence, host.Writer.Writes);
                Assert.IsEmpty(events);
                Assert.AreSame(defaultSessionBefore, TestSession.Default);
                Assert.AreEqual(1, host.DetectCapabilitiesCallCount);
                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
            })
            .Step("Menu pick: the menu's restore sequence precedes the banner, which lands on the normal screen buffer as the last writes", async context =>
            {
                var host = LiveHost();
                host.OnDelay = tick => host.Reader.Press(ConsoleKey.Enter);
                var events = new List<string>();

                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.IsEmpty(events);
                Assert.AreEqual(EnterSequence, host.Writer.Writes[0]);
                Assert.AreEqual(1, host.Writer.Writes.Count(write => write == RestoreSequence));
                Assert.AreEqual(host.Writer.Writes.Count - StartupBanner.Height - 1, host.Writer.Writes.ToList().IndexOf(RestoreSequence));
                AssertBannerIsLastWrites(host.Writer);
                // The menu's reader is the only one; the banner creates none.
                Assert.AreEqual(1, host.CreateTerminalReaderCallCount);
            })
            .Step("Prompt fallback pick on a redirected terminal: the banner follows the prompt as plain lines with no escape byte, the size never read, no reader beyond the prompt's", async context =>
            {
                var host = RedirectedHost();
                host.Reader.TypeLine("1");
                var events = new List<string>();

                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.IsEmpty(events);
                Assert.AreEqual(TestSelectionMenu.Prompt + Environment.NewLine, host.Writer.Writes[host.Writer.Writes.Count - StartupBanner.Height - 1]);
                AssertBannerIsLastWrites(host.Writer);
                foreach (var write in host.Writer.Writes)
                    Assert.DoesNotContain(AnsiCodes.Escape, write);

                Assert.AreEqual(1, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
            })
            .Step("Direct run on a redirected terminal: the plain banner with no escape byte, no reader, no size read", async context =>
            {
                var host = RedirectedHost();
                var events = new List<string>();

                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name=" + FakeTestName }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.HasCount(StartupBanner.Height, host.Writer.Writes);
                AssertBannerIsLastWrites(host.Writer);
                foreach (var write in host.Writer.Writes)
                    Assert.DoesNotContain(AnsiCodes.Escape, write);

                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
            })
            .Step("Discovery is asked for the given assembly, once", async context =>
            {
                var host = RedirectedHost();
                var discoverTests = new FakeDiscoverTests(FakeTestName);

                await new StandaloneRunnerCore(host, discoverTests).Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name=Does.Not.Exist" }, () => new FakeTestFrameworkAdapter(new List<string>()));

                var assembly = Assert.ContainsSingle(discoverTests.Assemblies);
                Assert.AreSame(typeof(StandaloneRunnerCoreTests).Assembly, assembly);
                Assert.ThrowsExactly<ArgumentNullException>(() => new StandaloneRunnerCore(host, null!));
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
