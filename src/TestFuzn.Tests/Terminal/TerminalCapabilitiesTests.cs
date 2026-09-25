using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class TerminalCapabilitiesTests : Test
{
    [Test]
    public async Task Verify_interactive_detection_from_redirect_flags()
    {
        await Scenario()
            .Step("Interactive when neither output nor input is redirected", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsTrue(capabilities.IsInteractive);
            })
            .Step("Not interactive when output is redirected", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: true,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsFalse(capabilities.IsInteractive);
            })
            .Step("Not interactive when input is redirected", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: true,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsFalse(capabilities.IsInteractive);
            })
            .Step("Not interactive when both output and input are redirected", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: true,
                    isInputRedirected: true,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsFalse(capabilities.IsInteractive);
            })
            .Run();
    }

    [Test]
    public async Task Verify_ansi_support_resolution()
    {
        await Scenario()
            .Step("Ansi supported on a normal terminal", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsTrue(capabilities.SupportsAnsi);
            })
            .Step("Ansi supported when TERM is not set", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: null,
                    colorTerm: null,
                    noColor: null);

                Assert.IsTrue(capabilities.SupportsAnsi);
            })
            .Step("Ansi supported when only input is redirected", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: true,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsTrue(capabilities.SupportsAnsi);
            })
            .Step("No ansi when output is redirected", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: true,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsFalse(capabilities.SupportsAnsi);
            })
            .Step("No ansi when TERM is dumb", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "dumb",
                    colorTerm: null,
                    noColor: null);

                Assert.IsFalse(capabilities.SupportsAnsi);
            })
            .Step("TERM is dumb is matched case-insensitively", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "DUMB",
                    colorTerm: null,
                    noColor: null);

                Assert.IsFalse(capabilities.SupportsAnsi);
            })
            .Step("No ansi when virtual terminal enablement failed", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: false,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsFalse(capabilities.SupportsAnsi);
            })
            .Run();
    }

    [Test]
    public async Task Verify_live_view_support_requires_interactive_ansi_terminal()
    {
        await Scenario()
            .Step("Live view supported on a normal interactive ansi terminal", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsTrue(capabilities.SupportsLiveView);
            })
            .Step("No live view when TERM is dumb even though the terminal is interactive", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "dumb",
                    colorTerm: null,
                    noColor: null);

                Assert.IsTrue(capabilities.IsInteractive);
                Assert.IsFalse(capabilities.SupportsLiveView);
            })
            .Step("No live view when output is redirected", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: true,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsFalse(capabilities.SupportsLiveView);
            })
            .Step("No live view when input is redirected even though ansi is supported", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: true,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.IsTrue(capabilities.SupportsAnsi);
                Assert.IsFalse(capabilities.SupportsLiveView);
            })
            .Run();
    }

    [Test]
    public async Task Verify_color_mode_resolution()
    {
        await Scenario()
            .Step("TrueColor when COLORTERM is truecolor", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: "truecolor",
                    noColor: null);

                Assert.AreEqual(ColorMode.TrueColor, capabilities.ColorMode);
            })
            .Step("TrueColor when COLORTERM is 24bit", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: "24bit",
                    noColor: null);

                Assert.AreEqual(ColorMode.TrueColor, capabilities.ColorMode);
            })
            .Step("COLORTERM is matched case-insensitively", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: "TrueColor",
                    noColor: null);

                Assert.AreEqual(ColorMode.TrueColor, capabilities.ColorMode);
            })
            .Step("Colors16 fallback when COLORTERM is not set", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: null);

                Assert.AreEqual(ColorMode.Colors16, capabilities.ColorMode);
            })
            .Step("Colors16 fallback when COLORTERM has an unrecognized value", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: "yes",
                    noColor: null);

                Assert.AreEqual(ColorMode.Colors16, capabilities.ColorMode);
            })
            .Step("No color when output is redirected even with COLORTERM set", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: true,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: "truecolor",
                    noColor: null);

                Assert.AreEqual(ColorMode.None, capabilities.ColorMode);
            })
            .Step("No color when TERM is dumb", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "dumb",
                    colorTerm: "truecolor",
                    noColor: null);

                Assert.AreEqual(ColorMode.None, capabilities.ColorMode);
            })
            .Step("No color when virtual terminal enablement failed", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: false,
                    term: "xterm-256color",
                    colorTerm: "truecolor",
                    noColor: null);

                Assert.AreEqual(ColorMode.None, capabilities.ColorMode);
            })
            .Run();
    }

    [Test]
    public async Task Verify_no_color_disables_color_but_not_ansi()
    {
        await Scenario()
            .Step("NO_COLOR set to a non-empty value resolves Monochrome, winning over COLORTERM", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: "truecolor",
                    noColor: "1");

                Assert.AreEqual(ColorMode.Monochrome, capabilities.ColorMode);
            })
            .Step("NO_COLOR keeps ansi support for cursor control", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: "truecolor",
                    noColor: "1");

                Assert.IsTrue(capabilities.SupportsAnsi);
            })
            .Step("NO_COLOR resolves Monochrome regardless of its value", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: "false");

                Assert.AreEqual(ColorMode.Monochrome, capabilities.ColorMode);
            })
            .Step("NO_COLOR set to an empty string does not disable color", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: false,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: "truecolor",
                    noColor: "");

                Assert.AreEqual(ColorMode.TrueColor, capabilities.ColorMode);
            })
            .Step("Without ansi support NO_COLOR still resolves None, never Monochrome", context =>
            {
                var capabilities = TerminalCapabilities.Resolve(
                    isOutputRedirected: true,
                    isInputRedirected: false,
                    isVirtualTerminalEnabled: true,
                    term: "xterm-256color",
                    colorTerm: null,
                    noColor: "1");

                Assert.IsFalse(capabilities.SupportsAnsi);
                Assert.AreEqual(ColorMode.None, capabilities.ColorMode);
            })
            .Run();
    }

    [Test]
    public async Task Verify_detect_reads_real_console_and_environment()
    {
        await Scenario()
            .Step("Detect resolves consistent capabilities from the real console state", context =>
            {
                var capabilities = TerminalCapabilities.Detect(new EnvironmentWrapper());

                Assert.IsNotNull(capabilities);
                if (!capabilities.SupportsAnsi)
                    Assert.AreEqual(ColorMode.None, capabilities.ColorMode);
            })
            .Step("Detect requires an environment", context =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => TerminalCapabilities.Detect(null!));
            })
            .Run();
    }
}
