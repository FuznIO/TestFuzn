using System.Globalization;
using System.Reflection;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Runner;

internal abstract class BaseTestFuznRunnerAdapter : ITestFrameworkAdapter, IDisposable
{
    internal const int DefaultWidth = 120;

    internal const int MinimumWidth = 40;

    private readonly CancellationTokenSource _cts = new();
    private readonly ILiveViewHost _liveViewHost;
    private readonly ConsoleCancelEventHandler _cancelKeyPressHandler;
    private TerminalCapabilities? _capabilities;
    private ITerminalWriter? _terminalWriter;
    private bool _isDisposed;

    protected BaseTestFuznRunnerAdapter()
        : this(new ConsoleLiveViewHost())
    {
    }

    protected BaseTestFuznRunnerAdapter(ILiveViewHost liveViewHost)
    {
        if (liveViewHost == null)
            throw new ArgumentNullException(nameof(liveViewHost), "Live view host cannot be null.");

        _liveViewHost = liveViewHost;

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

    public void Write(string message, params object?[] args)
    {
        var text = message;
        if (text == null)
            text = string.Empty;

        if (args != null && args.Length > 0)
            text = string.Format(CultureInfo.CurrentCulture, text, args);

        TerminalWriter.Write(text + Environment.NewLine);
    }

    public void WriteMarkup(string text)
    {
        TerminalWriter.Write(MarkupRenderer.Render(text, ColorMode) + Environment.NewLine);
    }

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

    public void WritePanel(string[] messages, string header)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages), "Messages cannot be null.");

        var contentWidth = 0;
        foreach (var message in messages)
            contentWidth = Math.Max(contentWidth, MarkupText.Measure(message));

        var naturalWidth = Math.Max(contentWidth + PanelWidget.ContentOverhead, MarkupText.Measure(header) + 5);
        var panelWidth = Math.Min(MeasureWidth(), naturalWidth);
        WriteLines(PanelWidget.Render(header, messages, panelWidth, ColorMode));
    }

    public void WriteAdvancedTable(AdvancedTable table)
    {
        WriteLines(AdvancedTableLayout.Render(table, MeasureWidth(), ColorMode));
    }

    public void WriteSummary(DateTime testRunStartDateTime, TimeSpan totalRunDuration, Dictionary<Scenario, ScenarioLoadResult> scenarioLoadResults, string reportPath, string executionEnvironment, string targetEnvironment)
    {
        if (scenarioLoadResults == null)
            throw new ArgumentNullException(nameof(scenarioLoadResults), "Scenario load results cannot be null.");

        var width = MeasureWidth();
        if (width < LoadSummaryLayout.MeasureMinimumWidth(scenarioLoadResults))
            width = DefaultWidth;

        var header = new LoadViewHeader { ExecutionEnvironment = executionEnvironment, TargetEnvironment = targetEnvironment };
        WriteLines(LoadSummaryLayout.Render(scenarioLoadResults, width, ColorMode, reportPath, header));
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

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Console.CancelKeyPress -= _cancelKeyPressHandler;
        _cts.Dispose();
    }

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
