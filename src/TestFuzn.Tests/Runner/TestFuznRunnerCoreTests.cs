using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Runner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.Runner;

[TestClass]
public class TestFuznRunnerCoreTests : Test
{
    private static readonly string UsageEvent = FakeTestFrameworkAdapter.MarkupEventPrefix + "[red]" + TestFuznRunnerCore.TestNameUsage + "[/]";

    private const string EnterSequence = AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap;
    private const string RestoreSequence = AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen;

    private const string FakeTestName = "Fuzn.Shop.Tests.CheckoutTests.Verify_checkout";

    private static readonly string BannerTitleWrite = TestFuznTestRunner.RunningTestLabel + " " + FakeTestName + Environment.NewLine;

    private static readonly string RejectionHeadlineWrite = "Exception: Test class 'NotATestClass' must implement ITest interface." + Environment.NewLine;

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

    private static TestFuznRunnerCore CoreOverFakeTest(FakeLiveViewHost host)
    {
        return new TestFuznRunnerCore(host, new FakeDiscoverTests(FakeTestName));
    }

    private static int AssertBannerThenRejection(FakeTerminalWriter writer, int bannerStart)
    {
        Assert.IsGreaterThan(bannerStart + StartupBanner.Height, writer.Writes.Count);
        Assert.AreEqual(BannerTitleWrite, writer.Writes[bannerStart]);

        var headline = bannerStart + StartupBanner.Height;
        Assert.AreEqual(RejectionHeadlineWrite, writer.Writes[headline]);
        Assert.IsGreaterThan(headline + 1, writer.Writes.Count);
        Assert.StartsWith("   at ", writer.Writes[headline + 1]);
        for (var index = headline; index < writer.Writes.Count; index++)
        {
            Assert.EndsWith(Environment.NewLine, writer.Writes[index]);
            Assert.DoesNotContain(AnsiCodes.Escape, writer.Writes[index]);
        }

        return headline;
    }

    [Test]
    public async Task Verify_a_test_name_without_a_value_is_an_invocation_error()
    {
        await Scenario()
            .Step("A bare --test-name exits 1 with the usage message written through the adapter, and nothing else", async context =>
            {
                var events = new List<string>();
                var exitCode = await new TestFuznRunnerCore().Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, new[] { "--test-name" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                var usage = Assert.ContainsSingle(events);
                Assert.AreEqual(UsageEvent, usage);
            })
            .Step("--test-name with a space instead of = is the same error, whatever follows", async context =>
            {
                var events = new List<string>();
                var exitCode = await new TestFuznRunnerCore().Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, new[] { "--test-name", "Fuzn.TestFuzn.Tests.SomeTests.Some_test" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                var usage = Assert.ContainsSingle(events);
                Assert.AreEqual(UsageEvent, usage);
            })
            .Step("A test name that names no test is still reported as not found with exit 1, without an adapter", async context =>
            {
                var events = new List<string>();
                var exitCode = await new TestFuznRunnerCore().Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, new[] { "--test-name=Fuzn.TestFuzn.Tests.SomeTests.Does_not_exist" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.IsEmpty(events);
            })
            .Step("Not found writes no banner: nothing goes through the host, whose terminal is never even asked for", async context =>
            {
                var host = LiveHost();
                var events = new List<string>();
                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, new[] { "--test-name=Does.Not.Exist" }, () => new FakeTestFrameworkAdapter(events));

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
            .Step("Direct run: the banner is the only terminal output before the run starts — no alternate screen, no reader, no size read — and the run then fails on its test class with exit 1, the failure written after the banner", async context =>
            {
                var host = LiveHost();
                var events = new List<string>();
                var defaultSessionBefore = TestSession.Default;

                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, new[] { "--test-name=" + FakeTestName }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                AssertBannerThenRejection(host.Writer, 0);
                Assert.DoesNotContain(EnterSequence, host.Writer.Writes);
                Assert.IsEmpty(events);
                Assert.AreSame(defaultSessionBefore, TestSession.Default);
                Assert.AreEqual(2, host.DetectCapabilitiesCallCount);
                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
            })
            .Step("Menu pick: the menu's restore sequence precedes the banner, which lands on the normal screen buffer, the failure after it", async context =>
            {
                var host = LiveHost();
                host.OnDelay = tick => host.Reader.Press(ConsoleKey.Enter);
                var events = new List<string>();

                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, Array.Empty<string>(), () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.IsEmpty(events);
                Assert.AreEqual(EnterSequence, host.Writer.Writes[0]);
                Assert.AreEqual(1, host.Writer.Writes.Count(write => write == RestoreSequence));
                var restore = host.Writer.Writes.ToList().IndexOf(RestoreSequence);
                AssertBannerThenRejection(host.Writer, restore + 1);
                Assert.AreEqual(1, host.CreateTerminalReaderCallCount);
            })
            .Step("Prompt fallback pick on a redirected terminal: the banner follows the prompt as plain lines with no escape byte, the size never read, no reader beyond the prompt's", async context =>
            {
                var host = RedirectedHost();
                host.Reader.TypeLine("1");
                var events = new List<string>();

                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, Array.Empty<string>(), () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.IsEmpty(events);
                var bannerStart = host.Writer.Writes.ToList().IndexOf(BannerTitleWrite);
                Assert.IsGreaterThan(0, bannerStart);
                Assert.AreEqual(TestSelectionMenu.Prompt + Environment.NewLine, host.Writer.Writes[bannerStart - 1]);
                AssertBannerThenRejection(host.Writer, bannerStart);
                foreach (var write in host.Writer.Writes)
                    Assert.DoesNotContain(AnsiCodes.Escape, write);

                Assert.AreEqual(1, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
            })
            .Step("Direct run on a redirected terminal: the plain banner and the plain failure with no escape byte, no reader, no size read", async context =>
            {
                var host = RedirectedHost();
                var events = new List<string>();

                var exitCode = await CoreOverFakeTest(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, new[] { "--test-name=" + FakeTestName }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                AssertBannerThenRejection(host.Writer, 0);
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

                await new TestFuznRunnerCore(host, discoverTests).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, new[] { "--test-name=Does.Not.Exist" }, () => new FakeTestFrameworkAdapter(new List<string>()));

                var assembly = Assert.ContainsSingle(discoverTests.Assemblies);
                Assert.AreSame(typeof(TestFuznRunnerCoreTests).Assembly, assembly);
                Assert.ThrowsExactly<ArgumentNullException>(() => new TestFuznRunnerCore(host, null!));
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

                var exitCode = await new TestFuznRunnerCore(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, Array.Empty<string>(), () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(0, exitCode);
                Assert.IsEmpty(events);
                Assert.AreEqual(TestSelectionMenu.Title + Environment.NewLine, host.Writer.Writes[0]);
                Assert.AreEqual(TestSelectionMenu.Prompt + Environment.NewLine, host.Writer.Writes[host.Writer.Writes.Count - 1]);
                Assert.Contains(write => write.Contains(typeof(TestFuznRunnerCoreTests).FullName + "." + nameof(Verify_the_selection_menu_runs_over_the_host_and_a_quit_exits_0), StringComparison.Ordinal), host.Writer.Writes);
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

                var exitCode = await new TestFuznRunnerCore(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, Array.Empty<string>(), () => new FakeTestFrameworkAdapter(events));

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

                var exitCode = await new TestFuznRunnerCore(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, Array.Empty<string>(), () => adapter);

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

                var run = new TestFuznRunnerCore(host).Run<FakeStartup>(typeof(TestFuznRunnerCoreTests).Assembly, Array.Empty<string>(), () => adapter);

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
