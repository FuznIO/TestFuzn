using System.Globalization;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Runner;

internal class TestSelectionMenu
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    internal const string Title = "TestFuzn Test Runner";

    internal const string Prompt = "Enter the ID of the test you want to run or enter to quit:";

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

    internal TestSelectionMenu(ILiveViewHost liveViewHost)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");

        _liveViewHost = liveViewHost;
    }

    public async Task<DiscoveredTest?> SelectTest(IReadOnlyList<DiscoveredTest> tests, CancellationToken cancellationToken)
    {
        if (tests == null)
            throw new ArgumentNullException(nameof(tests), "Tests cannot be null.");
        foreach (var test in tests)
        {
            if (test == null)
                throw new ArgumentNullException(nameof(tests), "Tests cannot contain null.");
        }

        var capabilities = _liveViewHost.DetectCapabilities();
        if (!capabilities.SupportsLiveView)
            return await SelectTestFromPrompt(tests, capabilities, cancellationToken);

        return await SelectTestFromMenu(tests, capabilities, cancellationToken);
    }

    private async Task<DiscoveredTest?> SelectTestFromMenu(IReadOnlyList<DiscoveredTest> tests, TerminalCapabilities capabilities, CancellationToken cancellationToken)
    {
        var terminalReader = _liveViewHost.CreateTerminalReader();
        if (terminalReader == null)
            throw new InvalidOperationException("The live view host returned no terminal reader.");

        var terminalWriter = _liveViewHost.CreateTerminalWriter();
        if (terminalWriter == null)
            throw new InvalidOperationException("The live view host returned no terminal writer.");

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
                var width = terminalWriter.WindowWidth;
                var height = terminalWriter.WindowHeight;
                state.VisibleRowCount = TestSelectionMenuLayout.MeasureListRowCount(width, height);

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
            try
            {
                terminalWriter.Write(AnsiCodes.Reset + AnsiCodes.EnableAutoWrap + AnsiCodes.ShowCursor + AnsiCodes.ExitAlternateScreen);
            }
            catch (Exception) when (menuFailure != null)
            {
            }
        }
    }

    private async Task<DiscoveredTest?> SelectTestFromPrompt(IReadOnlyList<DiscoveredTest> tests, TerminalCapabilities capabilities, CancellationToken cancellationToken)
    {
        var terminalReader = _liveViewHost.CreateTerminalReader();
        if (terminalReader == null)
            throw new InvalidOperationException("The live view host returned no terminal reader.");

        var terminalWriter = _liveViewHost.CreateTerminalWriter();
        if (terminalWriter == null)
            throw new InvalidOperationException("The live view host returned no terminal writer.");

        if (cancellationToken.IsCancellationRequested)
            return null;

        var colorMode = capabilities.ColorMode;

        WriteLine(terminalWriter, MarkupRenderer.Render(TitleMarkup, colorMode));
        foreach (var line in RenderNumberedList(tests, colorMode))
        {
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
