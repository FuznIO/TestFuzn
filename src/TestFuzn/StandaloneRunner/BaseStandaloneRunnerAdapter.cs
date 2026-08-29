using System.Globalization;
using System.Reflection;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.StandaloneRunner;

/// <summary>
/// The standalone runner's framework adapter: real-time console output is supported (the live
/// view runs), Ctrl+C cancels its token, and everything the framework writes through it — markup
/// lines, tables, panels, the standard test's result table and the load test's final summary —
/// renders on the terminal engine (<see cref="MarkupRenderer"/>, <see cref="TableWidget"/>,
/// <see cref="PanelWidget"/>, <see cref="AdvancedTableLayout"/>, <see cref="LoadSummaryLayout"/>)
/// and goes to the normal screen buffer through the terminal writer of an
/// <see cref="ILiveViewHost"/> — the real console in production, a fake in tests. The host's
/// capabilities, detected once on the first write, decide two things: the color mode — styled
/// only when the output is an ANSI terminal, plain text with zero escape bytes on a redirected
/// or non-ANSI output — and the layout width. The terminal's width is read only when the output
/// is an ANSI terminal (a redirected output has no width to read: Unix fabricates one and a
/// console-less Windows process throws), and only when it is at least
/// <see cref="MinimumWidth"/> columns; otherwise everything is laid out at
/// <see cref="DefaultWidth"/> and the terminal wraps whatever it cannot show. Tables and panels
/// never exceed the layout width; markup lines and plain writes are never truncated. Nothing
/// here ever queries the terminal — no cursor position, no key — so the summary cannot stall on
/// a terminal that does not answer, and every write is one call ending in a line break.
/// </summary>
internal abstract class BaseStandaloneRunnerAdapter : ITestFrameworkAdapter, IDisposable
{
    /// <summary>
    /// The width the summary, tables and panels are laid out at when the terminal's width is
    /// not used: on a redirected or non-ANSI output, and on a terminal narrower than
    /// <see cref="MinimumWidth"/>.
    /// </summary>
    internal const int DefaultWidth = 120;

    /// <summary>
    /// The narrowest terminal width the layouts use as given; below it — a size a pty reports
    /// as zero, a window too narrow to read a summary in — the terminal wraps a layout at
    /// <see cref="DefaultWidth"/> instead of the widgets degrading into unreadable fragments.
    /// </summary>
    internal const int MinimumWidth = 40;

    private readonly CancellationTokenSource _cts = new();
    private readonly ILiveViewHost _liveViewHost;
    private readonly ConsoleCancelEventHandler _cancelKeyPressHandler;
    private TerminalCapabilities? _capabilities;
    private ITerminalWriter? _terminalWriter;
    private bool _isDisposed;

    protected BaseStandaloneRunnerAdapter()
        : this(new ConsoleLiveViewHost())
    {
    }

    /// <param name="liveViewHost">The host whose terminal the adapter writes to: the real console in production, a fake in tests.</param>
    protected BaseStandaloneRunnerAdapter(ILiveViewHost liveViewHost)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");

        _liveViewHost = liveViewHost;

        // Ctrl+C cancels the token instead of ending the process, so the run winds down and
        // the summary is written. The handler is kept so Dispose can detach it: a handler left
        // behind would cancel a disposed source on the next Ctrl+C, and an exception thrown
        // inside a CancelKeyPress handler ends the process.
        _cancelKeyPressHandler = (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };
        Console.CancelKeyPress += _cancelKeyPressHandler;
    }

    public bool SupportsRealTimeConsoleOutput => true;
    public CancellationToken CancellationToken => _cts.Token;

    public abstract Task ExecuteTestMethod(ITest test, MethodInfo methodInfo);

    /// <summary>
    /// Writes the message as a plain line — formatted with the arguments, as a composite format
    /// string, only when there are any, so a message written on its own is never parsed for
    /// braces. Never truncated; line breaks in the message are written as they are.
    /// </summary>
    public void Write(string message, params object?[] args)
    {
        var text = message;
        if (text == null)
            text = string.Empty;

        if (args != null && args.Length > 0)
            text = string.Format(CultureInfo.CurrentCulture, text, args);

        TerminalWriter.Write(text + Environment.NewLine);
    }

    /// <summary>Writes the markup as one styled line, plain on a non-ANSI output; never truncated.</summary>
    public void WriteMarkup(string text)
    {
        TerminalWriter.Write(MarkupRenderer.Render(text, ColorMode) + Environment.NewLine);
    }

    /// <summary>
    /// Writes the table in a bordered box at its natural width, narrowed to the layout width
    /// when it is wider.
    /// </summary>
    public void WriteTable(TableData table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table), "Table cannot be null.");

        var columns = new List<TableColumn>();
        foreach (var column in table.Columns)
            columns.Add(new TableColumn(TextOrEmpty(column)));

        var rows = new List<IReadOnlyList<string?>>();
        foreach (var row in table.Rows)
            rows.Add(row);

        var width = MeasureWidth();
        var panelWidth = Math.Min(width, TableWidget.MeasureNaturalWidth(columns, rows) + PanelWidget.ContentOverhead);
        var content = TableWidget.Render(columns, rows, panelWidth - PanelWidget.ContentOverhead, ColorMode);
        WriteLines(PanelWidget.Render(null, content, panelWidth, ColorMode));
    }

    /// <summary>
    /// Writes the messages in a bordered box headed by the header, sized to its content and
    /// narrowed to the layout width when wider.
    /// </summary>
    public void WritePanel(string[] messages, string header)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages), "Messages cannot be null.");

        var contentWidth = 0;
        foreach (var message in messages)
            contentWidth = Math.Max(contentWidth, MarkupText.Measure(message));

        // The header sits in the top border with its own overhead ("╭─ " before, " ╮" after).
        var naturalWidth = Math.Max(contentWidth + PanelWidget.ContentOverhead, MarkupText.Measure(header) + 5);
        var panelWidth = Math.Min(MeasureWidth(), naturalWidth);
        WriteLines(PanelWidget.Render(header, messages, panelWidth, ColorMode));
    }

    /// <summary>Writes the table as a bordered box fitted to the layout width.</summary>
    public void WriteAdvancedTable(AdvancedTable table)
    {
        WriteLines(AdvancedTableLayout.Render(table, MeasureWidth(), ColorMode));
    }

    /// <summary>
    /// Writes the load test summary of every scenario, laid out at the layout width — or at
    /// <see cref="DefaultWidth"/> when the terminal is too narrow to show every number of the
    /// summary whole (even the layout's narrowest split of the response-time spread too wide),
    /// as below <see cref="MinimumWidth"/>: the terminal wraps, but no number is ever cut.
    /// </summary>
    public void WriteSummary(DateTime testRunStartDateTime, TimeSpan totalRunDuration, Dictionary<Scenario, ScenarioLoadResult> scenarioLoadResults)
    {
        if (scenarioLoadResults == null)
            throw new ArgumentNullException(nameof(scenarioLoadResults), "Scenario load results cannot be null.");

        var width = MeasureWidth();
        if (width < LoadSummaryLayout.MeasureMinimumWidth(scenarioLoadResults))
            width = DefaultWidth;

        WriteLines(LoadSummaryLayout.Render(scenarioLoadResults, width, ColorMode));
    }

    public string TestResultsDirectory
    {
        get
        {
            // Returns the entry assembly's directory so TestFuzn writes its
            // TestFuznResults folder there, matching the MSTest adapter which
            // places TestFuznResults as a sibling of MSTest's TestResults folder.
            var assemblyLocation = System.Reflection.Assembly.GetEntryAssembly().Location;
            return Path.GetDirectoryName(assemblyLocation)!;
        }
    }

    public void SetCurrentTestAsSkipped()
    {
        throw new ScenarioRunModeIgnoreException();
    }

    public void ThrowTestFuznIsNotInitializedException()
    {
        throw new NotImplementedException();
    }

    /// <summary>Detaches the Ctrl+C handler and disposes the token source; safe to call more than once.</summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Console.CancelKeyPress -= _cancelKeyPressHandler;
        _cts.Dispose();
    }

    // The capabilities are detected on the first write and kept: the output does not change
    // hands during a run.
    private TerminalCapabilities Capabilities
    {
        get
        {
            if (_capabilities == null)
                _capabilities = _liveViewHost.DetectCapabilities();

            return _capabilities;
        }
    }

    private ITerminalWriter TerminalWriter
    {
        get
        {
            if (_terminalWriter == null)
            {
                var terminalWriter = _liveViewHost.CreateTerminalWriter();
                if (terminalWriter == null)
                    throw new InvalidOperationException("The live view host returned no terminal writer.");

                _terminalWriter = terminalWriter;
            }

            return _terminalWriter;
        }
    }

    private ColorMode ColorMode => Capabilities.ColorMode;

    // The layout width for this write: the terminal's, read now so a resize between writes is
    // honored, when the output is an ANSI terminal wide enough; the default otherwise.
    private int MeasureWidth()
    {
        if (!Capabilities.SupportsAnsi)
            return DefaultWidth;

        var width = TerminalWriter.WindowWidth;
        if (width < MinimumWidth)
            return DefaultWidth;

        return width;
    }

    private void WriteLines(IReadOnlyList<RenderedLine> lines)
    {
        foreach (var line in lines)
            TerminalWriter.Write(line.Text + Environment.NewLine);
    }

    private static string TextOrEmpty(string? text)
    {
        if (text == null)
            return string.Empty;

        return text;
    }
}
