using System.Globalization;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.StandaloneRunner;

/// <summary>
/// The standalone runner's test selection: shown when no test is named, it returns the chosen
/// test or null when the user quits. On a terminal that supports the live view (interactive on
/// both input and output, ANSI capable) it is a full-screen menu on the alternate screen with
/// the cursor hidden — the logo, the numbered test list, a filter and a key-hint footer, laid
/// out by <see cref="TestSelectionMenuLayout"/> over a <see cref="TestSelectionMenuState"/> and
/// painted through the <see cref="FrameRenderer"/> diff — polled at <see cref="PollInterval"/>:
/// every tick reads the terminal size once, hands the list's rows to the state, drains the
/// keys pressed since the last tick into it, and repaints. Enter returns the highlighted test,
/// Escape returns null, and so does the cancellation token turning — Ctrl+C lands in the
/// framework adapter's token, which the poll loop watches, so the menu never blocks in a key
/// read and ends within a tick of the signal. The terminal is restored — styling reset,
/// auto-wrap re-enabled, cursor shown, alternate screen left — in one write, exactly once, on
/// a pick, a quit, a cancellation and an exception alike, so whatever follows (the "Running
/// test:" line, an error) lands on the normal screen buffer; when the menu itself failed, that
/// failure is the one reported even if the restore write fails too. On any other terminal — a
/// redirected output or input, a non-ANSI terminal — the prompt fallback keeps the previous
/// interaction contract: the title, a numbered list and the prompt are written as plain lines
/// (styled only when the output is an ANSI terminal), and lines are read until one is a listed
/// number, which selects that test; an empty line or the end of the input quits with null, and
/// anything else is reported as an invalid test index and prompted again. Each line is read on
/// a worker thread and raced against the token, so Ctrl+C at the prompt ends the fallback with
/// null as well — the adapter's Ctrl+C handler keeps the process alive, and a console read
/// blocked in the prompt would otherwise wait forever; the stranded read is left to the process
/// exit that follows. The fallback never reads the terminal size or a key, since a redirected
/// output has no size and a redirected input no keys. The terminal and the delay come from an
/// <see cref="ILiveViewHost"/>: the real console in production, fakes in tests.
/// </summary>
internal class TestSelectionMenu
{
    /// <summary>The key poll cadence of the full-screen menu: quick enough for typing to feel immediate.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>The prompt fallback's title line.</summary>
    internal const string Title = "TestFuzn Test Runner";

    /// <summary>The prompt fallback's prompt, written before every line read.</summary>
    internal const string Prompt = "Enter the ID of the test you want to run or enter to quit:";

    /// <summary>The prompt fallback's message for a line that is neither empty nor a listed number.</summary>
    internal const string InvalidTestIndexMessage = "Invalid test index.";

    private const string TitleMarkup = "[bold green]" + Title + "[/]";
    private const string PromptMarkup = "[bold green]" + Prompt + "[/]";
    private const string InvalidTestIndexMarkup = "[bold red]" + InvalidTestIndexMessage + "[/]";

    private static readonly TableColumn[] ListColumns =
    {
        new TableColumn("ID") { Alignment = TextAlignment.Right },
        new TableColumn("Test Name")
    };

    private readonly ILiveViewHost _liveViewHost;

    /// <param name="liveViewHost">The host the menu runs over: the real console in production, a fake in tests.</param>
    internal TestSelectionMenu(ILiveViewHost liveViewHost)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");

        _liveViewHost = liveViewHost;
    }

    /// <summary>
    /// Lets the user pick one of the tests, in the order given: the full-screen menu when the
    /// terminal supports the live view, the prompt fallback otherwise. Returns the chosen test,
    /// or null when the user quits or the token is cancelled.
    /// </summary>
    public async Task<DiscoveredTest?> SelectTest(IReadOnlyList<DiscoveredTest> tests, CancellationToken cancellationToken)
    {
        if (tests == null)
            throw new ArgumentNullException(nameof(tests), "Tests cannot be null.");
        foreach (var test in tests)
        {
            if (test == null)
                throw new ArgumentNullException(nameof(tests), "Tests cannot contain null.");
        }

        // Gate before anything reads the console dimensions or a key: a redirected output
        // fabricates a size, a console-less Windows process throws on reading it, and a
        // redirected input throws on a key read.
        var capabilities = _liveViewHost.DetectCapabilities();
        if (!capabilities.SupportsLiveView)
            return await SelectTestFromPrompt(tests, capabilities, cancellationToken);

        return await SelectTestFromMenu(tests, capabilities, cancellationToken);
    }

    private async Task<DiscoveredTest?> SelectTestFromMenu(IReadOnlyList<DiscoveredTest> tests, TerminalCapabilities capabilities, CancellationToken cancellationToken)
    {
        // A host without a reader or a writer fails here, loud, before the alternate screen.
        var terminalReader = _liveViewHost.CreateTerminalReader();
        if (terminalReader == null)
            throw new InvalidOperationException("The live view host returned no terminal reader.");

        var terminalWriter = _liveViewHost.CreateTerminalWriter();
        if (terminalWriter == null)
            throw new InvalidOperationException("The live view host returned no terminal writer.");

        // Already cancelled: nothing to show, nothing written.
        if (cancellationToken.IsCancellationRequested)
            return null;

        var testNames = new string[tests.Count];
        for (var index = 0; index < tests.Count; index++)
            testNames[index] = tests[index].Name;

        var state = new TestSelectionMenuState(testNames);
        var frame = new FrameBuffer();
        var renderer = new FrameRenderer(terminalWriter);

        terminalWriter.Write(AnsiCodes.EnterAlternateScreen + AnsiCodes.HideCursor + AnsiCodes.DisableAutoWrap);
        Exception? menuFailure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // One size reading per tick, shared by the state's window, the layout and the
                // renderer, so the three always agree on the size even if the terminal resizes
                // between them.
                var width = terminalWriter.WindowWidth;
                var height = terminalWriter.WindowHeight;
                state.VisibleRowCount = TestSelectionMenuLayout.MeasureListRowCount(width, height);

                // The reader never blocks, so every key pressed since the last tick is applied
                // before the repaint; a pick or a quit ends the menu without painting again.
                while (terminalReader.TryReadKey(out var key))
                {
                    var outcome = state.HandleKey(key);
                    if (outcome == TestSelectionMenuOutcome.Quit)
                        return null;

                    if (outcome == TestSelectionMenuOutcome.TestSelected)
                    {
                        var highlightedTest = state.HighlightedTest;
                        if (highlightedTest == null)
                            throw new InvalidOperationException("The menu reported a selection without a highlighted test.");

                        return tests[highlightedTest.Value];
                    }
                }

                frame.Clear();
                frame.AddLines(TestSelectionMenuLayout.Render(state, width, height, capabilities.ColorMode));
                renderer.Render(frame, width, height);

                // Returns normally on cancellation (DelayHelper.Delay swallows the
                // TaskCanceledException); the loop condition then ends the menu.
                await _liveViewHost.Delay(PollInterval, cancellationToken);
            }

            return null;
        }
        catch (Exception exception)
        {
            menuFailure = exception;
            throw;
        }
        finally
        {
            // The restore is attempted on every exit path. When the menu itself failed, that
            // failure is the one reported: a restore that fails too — the same broken writer,
            // most likely — must not replace it (as the console manager's RestoreTerminal keeps
            // the first failure); with no failure of the menu's own, a failing restore is reported.
            try
            {
                terminalWriter.Write(AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen);
            }
            catch (Exception) when (menuFailure != null)
            {
            }
        }
    }

    // The previous interaction contract on plain lines: the title, the numbered list, the
    // prompt, then one line read per attempt. The writer is only ever written to — its size is
    // never read — and the reader is only ever asked for lines, which a redirected input answers.
    private async Task<DiscoveredTest?> SelectTestFromPrompt(IReadOnlyList<DiscoveredTest> tests, TerminalCapabilities capabilities, CancellationToken cancellationToken)
    {
        var terminalReader = _liveViewHost.CreateTerminalReader();
        if (terminalReader == null)
            throw new InvalidOperationException("The live view host returned no terminal reader.");

        var terminalWriter = _liveViewHost.CreateTerminalWriter();
        if (terminalWriter == null)
            throw new InvalidOperationException("The live view host returned no terminal writer.");

        // Already cancelled: nothing to show, nothing written.
        if (cancellationToken.IsCancellationRequested)
            return null;

        var colorMode = capabilities.ColorMode;

        WriteLine(terminalWriter, MarkupRenderer.Render(TitleMarkup, colorMode));
        foreach (var line in RenderNumberedList(tests, colorMode))
        {
            // The table pads every row to its width; on plain lines that padding is noise.
            WriteLine(terminalWriter, line.Text.TrimEnd(' '));
        }

        WriteLine(terminalWriter, string.Empty);
        WriteLine(terminalWriter, MarkupRenderer.Render(PromptMarkup, colorMode));

        while (true)
        {
            var input = await ReadLine(terminalReader, cancellationToken);
            if (string.IsNullOrEmpty(input))
                return null;

            if (int.TryParse(input, out var testNumber) && testNumber >= 1 && testNumber <= tests.Count)
                return tests[testNumber - 1];

            WriteLine(terminalWriter, MarkupRenderer.Render(InvalidTestIndexMarkup, colorMode));
            WriteLine(terminalWriter, MarkupRenderer.Render(PromptMarkup, colorMode));
        }
    }

    // Reads one line on a worker thread, raced against the token: the line (null at the end of
    // the input) when the read completes first, null when the token is cancelled first — the
    // read then stays blocked on its worker until the console read returns, unobserved, which
    // the process exit that follows a cancellation does not wait for. A read that fails
    // propagates as is; the prompt is never continued over a broken input.
    private static async Task<string?> ReadLine(ITerminalReader terminalReader, CancellationToken cancellationToken)
    {
        var read = Task.Run(() => terminalReader.ReadLine());
        try
        {
            return await read.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    // The ID and name columns at the table's natural width, so nothing is ever truncated:
    // the fallback has no terminal width to fit.
    private static IReadOnlyList<RenderedLine> RenderNumberedList(IReadOnlyList<DiscoveredTest> tests, ColorMode colorMode)
    {
        var rows = new List<IReadOnlyList<string?>>(tests.Count);
        for (var index = 0; index < tests.Count; index++)
            rows.Add(new[] { (index + 1).ToString(CultureInfo.InvariantCulture), MarkupParser.Escape(tests[index].Name) });

        return TableWidget.Render(ListColumns, rows, TableWidget.MeasureNaturalWidth(ListColumns, rows), colorMode);
    }

    private static void WriteLine(ITerminalWriter terminalWriter, string text)
    {
        terminalWriter.Write(text + Environment.NewLine);
    }
}
