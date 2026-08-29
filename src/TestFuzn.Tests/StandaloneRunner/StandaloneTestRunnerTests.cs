using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.StandaloneRunner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.StandaloneRunner;

/// <summary>
/// Pins <see cref="StandaloneTestRunner.RunTest{TStartup}"/>'s startup banner: written through
/// the live view host's terminal as the very first thing — before the test class is even
/// instantiated, so before the session initializes — styled by the detected color mode, as
/// plain lines with no escape byte on a redirected or non-ANSI output, the terminal's size
/// never read and no key reader created; its detail line names the test's assembly and the
/// target environment the session will initialize with (the environment variable, since the
/// session is given no arguments), or "-" when none is set. The runs here name a class that is
/// not a test class, which the runner rejects right after the banner: the process-wide default
/// session — which the suite's own tests resolve in parallel — is never touched.
/// </summary>
[TestClass]
public class StandaloneTestRunnerTests : Test
{
    private const string TestName = "Fuzn.Shop.Tests.CheckoutTests.Verify_checkout";
    private const string AssemblyName = "Fuzn.TestFuzn.Tests";

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

    /// <summary>An environment with the target environment variable set to the value, or unset for null.</summary>
    private static FakeEnvironmentWrapper EnvironmentWith(string? targetEnvironment)
    {
        var environment = new FakeEnvironmentWrapper();
        if (targetEnvironment != null)
            environment.Variables[TestSession.TargetEnvironmentVariable] = targetEnvironment;

        return environment;
    }

    /// <summary>Runs the fake test through the runner, which rejects its class right after the banner.</summary>
    private static async Task<Exception> RunRejectedTest(FakeLiveViewHost host, FakeEnvironmentWrapper environment, List<string> events)
    {
        var runner = new StandaloneTestRunner(host, environment);
        return await Assert.ThrowsExactlyAsync<Exception>(async () => await runner.RunTest<FakeStartup>(new[] { "run" }, new FakeTestFrameworkAdapter(events), FakeDiscoverTests.Test(TestName)));
    }

    private static List<string> ExpectedBannerWrites(ColorMode colorMode, string targetEnvironment)
    {
        return StartupBanner.Render(StandaloneTestRunner.RunningTestLabel, TestName, StandaloneTestRunner.FormatDetailLine(AssemblyName, targetEnvironment), colorMode)
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

                var rejection = await RunRejectedTest(host, EnvironmentWith("staging"), events);

                Assert.AreEqual("Test class 'NotATestClass' must implement ITest interface.", rejection.Message);
                CollectionAssert.AreEqual(ExpectedBannerWrites(ColorMode.TrueColor, "staging"), host.Writer.Writes.ToList());
                Assert.AreSame(defaultSessionBefore, TestSession.Default);
                Assert.IsEmpty(events);

                Assert.AreEqual(1, host.DetectCapabilitiesCallCount);
                Assert.AreEqual(1, host.CreateTerminalWriterCallCount);
                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.Writer.WindowHeightReadCount);
                Assert.AreEqual(0, host.DelayCount);
            })
            .Step("On a redirected output the same three lines are plain, with no escape byte, the size never read and no reader created", async context =>
            {
                var host = RedirectedHost();
                Assert.AreEqual(ColorMode.None, host.Capabilities.ColorMode);
                var events = new List<string>();

                await RunRejectedTest(host, EnvironmentWith("staging"), events);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "⚡ TestFuzn" + Environment.NewLine,
                        "Running test: Fuzn.Shop.Tests.CheckoutTests.Verify_checkout" + Environment.NewLine,
                        "Assembly: Fuzn.TestFuzn.Tests · Target environment: staging" + Environment.NewLine
                    },
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

                await RunRejectedTest(host, EnvironmentWith(null), new List<string>());

                CollectionAssert.AreEqual(ExpectedBannerWrites(ColorMode.None, string.Empty), host.Writer.Writes.ToList());
                Assert.AreEqual("Assembly: Fuzn.TestFuzn.Tests · Target environment: -" + Environment.NewLine, host.Writer.Writes[2]);
                Assert.AreEqual(0, host.Writer.WindowWidthReadCount);
                Assert.AreEqual(0, host.CreateTerminalReaderCallCount);
            })
            .Step("Colors16 downgrades the accents to bright yellow", async context =>
            {
                var host = LiveHost(ColorMode.Colors16);

                await RunRejectedTest(host, EnvironmentWith("test"), new List<string>());

                CollectionAssert.AreEqual(ExpectedBannerWrites(ColorMode.Colors16, "test"), host.Writer.Writes.ToList());
                Assert.StartsWith(AnsiCodes.Foreground(ConsoleColor.Yellow) + "⚡", host.Writer.Writes[0]);
                Assert.StartsWith(AnsiCodes.Csi + "1;93m" + StandaloneTestRunner.RunningTestLabel, host.Writer.Writes[1]);
            })
            .Run();
    }

    [Test]
    public async Task Verify_the_detail_line_names_the_assembly_and_the_target_environment()
    {
        await Scenario()
            .Step("The target environment comes from the environment variable, as the session reads it; unset or empty shows as a dash", async context =>
            {
                var unset = LiveHost(ColorMode.None);
                await RunRejectedTest(unset, EnvironmentWith(null), new List<string>());
                Assert.AreEqual("Assembly: Fuzn.TestFuzn.Tests · Target environment: -" + Environment.NewLine, unset.Writer.Writes[2]);

                var empty = LiveHost(ColorMode.None);
                await RunRejectedTest(empty, EnvironmentWith(string.Empty), new List<string>());
                Assert.AreEqual("Assembly: Fuzn.TestFuzn.Tests · Target environment: -" + Environment.NewLine, empty.Writer.Writes[2]);

                var production = LiveHost(ColorMode.None);
                await RunRejectedTest(production, EnvironmentWith("production"), new List<string>());
                Assert.AreEqual("Assembly: Fuzn.TestFuzn.Tests · Target environment: production" + Environment.NewLine, production.Writer.Writes[2]);
            })
            .Step("FormatDetailLine composes the line; a missing assembly name shows as the same dash instead of failing the run — the banner is decoration", context =>
            {
                Assert.AreEqual("Assembly: Fuzn.Shop.Tests · Target environment: staging", StandaloneTestRunner.FormatDetailLine("Fuzn.Shop.Tests", "staging"));
                Assert.AreEqual("Assembly: Fuzn.Shop.Tests · Target environment: -", StandaloneTestRunner.FormatDetailLine("Fuzn.Shop.Tests", null));
                Assert.AreEqual("Assembly: Fuzn.Shop.Tests · Target environment: -", StandaloneTestRunner.FormatDetailLine("Fuzn.Shop.Tests", string.Empty));
                Assert.AreEqual("Assembly: - · Target environment: staging", StandaloneTestRunner.FormatDetailLine(null, "staging"));
                Assert.AreEqual("Assembly: - · Target environment: -", StandaloneTestRunner.FormatDetailLine(string.Empty, string.Empty));
                Assert.AreEqual("-", StandaloneTestRunner.UnsetValueText);
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
                var runner = new StandaloneTestRunner(host, EnvironmentWith(null));

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await runner.RunTest<FakeStartup>(new[] { "run" }, new FakeTestFrameworkAdapter(new List<string>()), FakeDiscoverTests.Test(TestName)));

                Assert.AreSame(defaultSessionBefore, TestSession.Default);
                Assert.AreEqual(1, host.CreateTerminalWriterCallCount);
            })
            .Step("Null and incomplete arguments are rejected", async context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => new StandaloneTestRunner(null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => new StandaloneTestRunner(LiveHost(ColorMode.None), null!));

                var runner = new StandaloneTestRunner(LiveHost(ColorMode.None), EnvironmentWith(null));
                await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await runner.RunTest<FakeStartup>(new[] { "run" }, null!, FakeDiscoverTests.Test(TestName)));
                await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await runner.RunTest<FakeStartup>(new[] { "run" }, new FakeTestFrameworkAdapter(new List<string>()), null!));
                await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await runner.RunTest<FakeStartup>(new[] { "run" }, new FakeTestFrameworkAdapter(new List<string>()), new DiscoveredTest { Name = TestName }));
            })
            .Run();
    }
}
