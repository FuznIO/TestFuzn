using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class AnsiCodesTests : Test
{
    [Test]
    public async Task Verify_sgr_styling_constants()
    {
        var expectedStylingSequences = new Dictionary<string, (string Expected, string Actual)>
        {
            { "Reset", ("\u001b[0m", AnsiCodes.Reset) },
            { "Bold", ("\u001b[1m", AnsiCodes.Bold) },
            { "Dim", ("\u001b[2m", AnsiCodes.Dim) },
            { "Italic", ("\u001b[3m", AnsiCodes.Italic) },
            { "Underline", ("\u001b[4m", AnsiCodes.Underline) },
            { "Reverse", ("\u001b[7m", AnsiCodes.Reverse) },
            { "Strikethrough", ("\u001b[9m", AnsiCodes.Strikethrough) },
            { "DefaultForeground", ("\u001b[39m", AnsiCodes.DefaultForeground) },
            { "DefaultBackground", ("\u001b[49m", AnsiCodes.DefaultBackground) }
        };

        await Scenario()
            .Step("Styling constants emit the expected SGR sequences", context =>
            {
                AssertSequences(expectedStylingSequences);
            })
            .Run();
    }

    [Test]
    public async Task Verify_screen_and_cursor_mode_constants()
    {
        var expectedSynchronizedOutputSequences = new Dictionary<string, (string Expected, string Actual)>
        {
            { "BeginSynchronizedOutput", ("\u001b[?2026h", AnsiCodes.BeginSynchronizedOutput) },
            { "EndSynchronizedOutput", ("\u001b[?2026l", AnsiCodes.EndSynchronizedOutput) }
        };

        var expectedAlternateScreenSequences = new Dictionary<string, (string Expected, string Actual)>
        {
            { "EnterAlternateScreen", ("\u001b[?1049h", AnsiCodes.EnterAlternateScreen) },
            { "ExitAlternateScreen", ("\u001b[?1049l", AnsiCodes.ExitAlternateScreen) }
        };

        var expectedCursorVisibilitySequences = new Dictionary<string, (string Expected, string Actual)>
        {
            { "ShowCursor", ("\u001b[?25h", AnsiCodes.ShowCursor) },
            { "HideCursor", ("\u001b[?25l", AnsiCodes.HideCursor) }
        };

        var expectedAutoWrapSequences = new Dictionary<string, (string Expected, string Actual)>
        {
            { "EnableAutoWrap", ("\u001b[?7h", AnsiCodes.EnableAutoWrap) },
            { "DisableAutoWrap", ("\u001b[?7l", AnsiCodes.DisableAutoWrap) }
        };

        var expectedEraseSequences = new Dictionary<string, (string Expected, string Actual)>
        {
            { "EraseLine", ("\u001b[2K", AnsiCodes.EraseLine) },
            { "EraseToEndOfLine", ("\u001b[K", AnsiCodes.EraseToEndOfLine) },
            { "EraseScreen", ("\u001b[2J", AnsiCodes.EraseScreen) }
        };

        await Scenario()
            .Step("Synchronized output uses DEC private mode 2026", context =>
            {
                AssertSequences(expectedSynchronizedOutputSequences);
            })
            .Step("Alternate screen uses DEC private mode 1049", context =>
            {
                AssertSequences(expectedAlternateScreenSequences);
            })
            .Step("Cursor visibility uses DEC private mode 25", context =>
            {
                AssertSequences(expectedCursorVisibilitySequences);
            })
            .Step("Auto-wrap uses DEC private mode 7 (DECAWM)", context =>
            {
                AssertSequences(expectedAutoWrapSequences);
            })
            .Step("Erase operations emit the expected sequences", context =>
            {
                AssertSequences(expectedEraseSequences);
            })
            .Run();
    }

    [Test]
    public async Task Verify_cursor_addressing()
    {
        var expectedCursorAddressingSequences = new Dictionary<string, (string Expected, string Actual)>
        {
            { "MoveCursor(1, 1)", ("\u001b[1;1H", AnsiCodes.MoveCursor(1, 1)) },
            { "MoveCursor(24, 80)", ("\u001b[24;80H", AnsiCodes.MoveCursor(24, 80)) },
            { "CursorHome", ("\u001b[H", AnsiCodes.CursorHome) }
        };

        await Scenario()
            .Step("MoveCursor emits a 1-based row and column address", context =>
            {
                AssertSequences(expectedCursorAddressingSequences);
            })
            .Step("MoveCursor rejects rows and columns below 1", context =>
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AnsiCodes.MoveCursor(0, 1));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AnsiCodes.MoveCursor(1, 0));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AnsiCodes.MoveCursor(-1, -1));
            })
            .Run();
    }

    [Test]
    public async Task Verify_truecolor_sequences()
    {
        await Scenario()
            .Step("Foreground truecolor uses SGR 38;2 with RGB components", context =>
            {
                Assert.AreEqual("\u001b[38;2;255;128;0m", AnsiCodes.ForegroundTrueColor(255, 128, 0));
                Assert.AreEqual("\u001b[38;2;0;0;0m", AnsiCodes.ForegroundTrueColor(0, 0, 0));
            })
            .Step("Background truecolor uses SGR 48;2 with RGB components", context =>
            {
                Assert.AreEqual("\u001b[48;2;30;30;46m", AnsiCodes.BackgroundTrueColor(30, 30, 46));
                Assert.AreEqual("\u001b[48;2;255;255;255m", AnsiCodes.BackgroundTrueColor(255, 255, 255));
            })
            .Run();
    }

    [Test]
    public async Task Verify_16_color_sequences()
    {
        var expectedForegroundCodes = new Dictionary<ConsoleColor, int>
        {
            { ConsoleColor.Black, 30 },
            { ConsoleColor.DarkRed, 31 },
            { ConsoleColor.DarkGreen, 32 },
            { ConsoleColor.DarkYellow, 33 },
            { ConsoleColor.DarkBlue, 34 },
            { ConsoleColor.DarkMagenta, 35 },
            { ConsoleColor.DarkCyan, 36 },
            { ConsoleColor.Gray, 37 },
            { ConsoleColor.DarkGray, 90 },
            { ConsoleColor.Red, 91 },
            { ConsoleColor.Green, 92 },
            { ConsoleColor.Yellow, 93 },
            { ConsoleColor.Blue, 94 },
            { ConsoleColor.Magenta, 95 },
            { ConsoleColor.Cyan, 96 },
            { ConsoleColor.White, 97 }
        };

        await Scenario()
            .Step("Every console color maps to its ANSI foreground and background code", context =>
            {
                Assert.HasCount(16, expectedForegroundCodes);

                foreach (var pair in expectedForegroundCodes)
                {
                    Assert.AreEqual($"\u001b[{pair.Value}m", AnsiCodes.Foreground(pair.Key), $"Foreground mismatch for {pair.Key}");
                    Assert.AreEqual($"\u001b[{pair.Value + 10}m", AnsiCodes.Background(pair.Key), $"Background mismatch for {pair.Key}");
                }
            })
            .Step("Unknown console color values are rejected", context =>
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AnsiCodes.Foreground((ConsoleColor)999));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AnsiCodes.Background((ConsoleColor)999));
            })
            .Run();
    }

    private static void AssertSequences(Dictionary<string, (string Expected, string Actual)> expectedSequences)
    {
        Assert.IsNotEmpty(expectedSequences);

        foreach (var pair in expectedSequences)
            Assert.AreEqual(pair.Value.Expected, pair.Value.Actual, $"Sequence mismatch for {pair.Key}");
    }
}
