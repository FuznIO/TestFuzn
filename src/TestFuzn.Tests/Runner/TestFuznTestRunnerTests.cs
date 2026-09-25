using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Runner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.Runner;

[TestClass]
public class TestFuznTestRunnerTests : Test
{
    private const string TestName = "Fuzn.Shop.Tests.CheckoutTests.Verify_checkout";

    private static FakeLiveViewHost LiveHost(ColorMode colorMode)
    {
        var host = new FakeLiveViewHost(new TerminalCapabilities(isInteractive: true, supportsAnsi: true, colorMode: colorMode));
        host.Writer.WindowWidth = 120;
        host.Writer.WindowHeight = 40;
        return host;
    }

    private static FakeLiveViewHost RedirectedHost()
    {
        return new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: true, isInputRedirected: true, isVirtualTerminalEnabled: true, term: "xterm-256color", colorTerm: "truecolor", noColor: null));
    }

    private static async Task<Exception> RunRejectedTest(FakeLiveViewHost host, List<string> events)
    {
        var runner = new TestFuznTestRunner(host);
        return await Assert.ThrowsExactlyAsync<Exception>(async () => await runner.RunTest<FakeStartup>(Array.Empty<string>(), new FakeTestFrameworkAdapter(events), FakeDiscoverTests.Test(TestName)));
    }

    private static List<string> ExpectedBannerWrites(ColorMode colorMode)
    {
        return StartupBanner.Render(TestFuznTestRunner.RunningTestLabel, TestName, colorMode)
            .Select(line => line.Text + Environment.NewLine)
            .ToList();
    }

    [Test]
    public async Task Verify_the_banner_is_written_first_through_the_host()
    {
        await Scenario()
            .Step("On a TrueColor terminal the styled banner is the only terminal output before the run fails on its test class; the default session is untouched", async context =>
            {
                var host = LiveHost(ColorMode.TrueColor);
                var events = new List<string>();
                var defaultSessionBefore = TestSession.Default;

                var rejection = await RunRejectedTest(host, events);

                Assert.AreEqual("Test class 'NotATestClass' must implement ITest interface.", rejection.Message);
                CollectionAssert.AreEqual(ExpectedBannerWrites(ColorMode.TrueColor), host.Writer.Writes.ToList());
                Assert.AreSame(defaultSessionBefore, TestSession.Default);
                Assert.IsEmpty(events);

                Assert.AreEqual(1, host.DetectCapabilitiesCallCount);
                Assert.AreEqual(1, host.CreateTerminalWriterCallCount);
                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
                Assert.AreEqual(0, host.DelayCount);
            })
            .Step("On a redirected output the same line is plain, with no escape byte, the size never read and no reader created", async context =>
            {
                var host = RedirectedHost();
                Assert.AreEqual(ColorMode.None, host.Capabilities.ColorMode);
                var events = new List<string>();

                await RunRejectedTest(host, events);

                CollectionAssert.AreEqual(
                    new[] { "Running test: Fuzn.Shop.Tests.CheckoutTests.Verify_checkout" + Environment.NewLine },
                    host.Writer.Writes.ToList());
                foreach (var write in host.Writer.Writes)
                    Assert.DoesNotContain(AnsiCodes.Escape, write);

                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
                Assert.IsEmpty(events);
            })
            .Step("A dumb terminal — interactive but non-ANSI — gets the plain banner too", async context =>
            {
                var host = new FakeLiveViewHost(TerminalCapabilities.Resolve(isOutputRedirected: false, isInputRedirected: false, isVirtualTerminalEnabled: true, term: "dumb", colorTerm: "truecolor", noColor: null));
                Assert.IsTrue(host.Capabilities.IsInteractive);
                Assert.AreEqual(ColorMode.None, host.Capabilities.ColorMode);

                await RunRejectedTest(host, new List<string>());

                CollectionAssert.AreEqual(ExpectedBannerWrites(ColorMode.None), host.Writer.Writes.ToList());
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
            })
            .Step("Colors16 downgrades the accents to bright yellow", async context =>
            {
                var host = LiveHost(ColorMode.Colors16);

                await RunRejectedTest(host, new List<string>());

                CollectionAssert.AreEqual(ExpectedBannerWrites(ColorMode.Colors16), host.Writer.Writes.ToList());
                Assert.StartsWith(AnsiCodes.Csi + "1;93m" + TestFuznTestRunner.RunningTestLabel, host.Writer.Writes[0]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_guards_fail_before_anything_of_the_run_starts()
    {
        await Scenario()
            .Step("A host without a terminal writer fails loud before the run, the default session untouched", async context =>
            {
                var host = LiveHost(ColorMode.None);
                host.HasWriter = false;
                var defaultSessionBefore = TestSession.Default;
                var runner = new TestFuznTestRunner(host);

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await runner.RunTest<FakeStartup>(Array.Empty<string>(), new FakeTestFrameworkAdapter(new List<string>()), FakeDiscoverTests.Test(TestName)));

                Assert.AreSame(defaultSessionBefore, TestSession.Default);
                Assert.AreEqual(1, host.CreateTerminalWriterCallCount);
            })
            .Step("Null and incomplete arguments are rejected", async context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => new TestFuznTestRunner(null!));

                var runner = new TestFuznTestRunner(LiveHost(ColorMode.None));
                await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await runner.RunTest<FakeStartup>(Array.Empty<string>(), null!, FakeDiscoverTests.Test(TestName)));
                await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await runner.RunTest<FakeStartup>(Array.Empty<string>(), new FakeTestFrameworkAdapter(new List<string>()), null!));
                await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await runner.RunTest<FakeStartup>(Array.Empty<string>(), new FakeTestFrameworkAdapter(new List<string>()), new DiscoveredTest { Name = TestName }));
            })
            .Run();
    }
}
