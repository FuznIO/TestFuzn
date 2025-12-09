using Spectre.Console;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.ConsoleOutput;

internal class LiveLoadTestDisplay(Dictionary<string, LiveMetrics> stats, CancellationToken stoppingToken)
{
    private Dictionary<string, LiveMetrics> _stats = stats;
    //private List<Table> _stepTables = new();
    public bool KeepRunning { get; set; } = true;

    public Task Show()
    {
        AnsiConsole.Clear();
        var metaTable = BuildMetaDataTable();
        var statsTable = BuildStatsTable();
        var stepsTable = BuildStepsTable();

        var grid = new Grid().AddColumn().AddColumn().AddColumn();
        grid.AddRow(new Panel(metaTable)
            .Header("[bold white on blue] Metadata [/]"))
        .AddRow(new Panel(statsTable)
            .Header("[bold white on green] Global Stats [/]")
            .Expand())
        .AddRow(new Panel(stepsTable)
            .Header("[bold white on green] Steps Stats [/]")
            .Expand());

        return AnsiConsole.Live(grid)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async ctx =>
            {
                while (KeepRunning)
                {
                    await DelayHelper.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                    UpdateMetaDataTable(metaTable);
                    UpdateStatsTable(statsTable);
                    UpdateStepsTables(stepsTable);
                    ctx.Refresh();
                }

                ctx.Refresh();
                
                return Task.CompletedTask;
            });
    }

    public void UpdateStats(Dictionary<string, LiveMetrics> updatedStats)
    {
        _stats = updatedStats;
    }

    private Table BuildMetaDataTable()
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn(new TableColumn("[yellow]Scenario[/]").Centered());
        table.AddColumn(new TableColumn("[yellow]Duration[/]").Centered());
        table.AddColumn(new TableColumn("[yellow]Status[/]").Centered());
        
        foreach (var scenarioStats in _stats)
        {
            TimeSpan rounded = TimeSpan.FromSeconds(Math.Ceiling(scenarioStats.Value.ScenarioLoadResultSnapshot.TotalExecutionDuration.TotalSeconds));
            table.AddRow($"{scenarioStats.Key}", $"{(int)rounded.TotalHours:00}:{rounded.Minutes:00}:{rounded.Seconds:00}", $"Running");
        }
        
        return table;
    }

    private Table BuildStatsTable()
    {
        var table = new Table { Border = TableBorder.Minimal };
        table.AddColumn("[u]Scenario[/]");
        table.AddColumn("[u]Metric[/]");
        table.AddColumn("[u]Count[/]");
        table.AddColumn("[u]RPS[/]");
        table.AddColumn("[u]Min[/]");
        table.AddColumn("[u]Mean[/]");
        table.AddColumn("[u]Max[/]");
        table.AddColumn("[u]StdDev[/]");
        table.AddColumn("[u]Median[/]");
        table.AddColumn("[u]P75[/]");
        table.AddColumn("[u]P95[/]");
        table.AddColumn("[u]P99[/]");

        foreach (var scenarioStats in _stats)
        {
            table.AddRow($"{scenarioStats.Key}", "Total", $"{scenarioStats.Value.ScenarioLoadResultSnapshot.RequestCount}");
            table.AddRow("", "[green]OK[/]", $"{scenarioStats.Value.ScenarioLoadResultSnapshot.Ok.RequestCount}");
            table.AddRow("", "[red]Failed[/]", $"{scenarioStats.Value.ScenarioLoadResultSnapshot.Failed.RequestCount}", "—");
        }

        return table;
    }

    private void UpdateMetaDataTable(Table table)
    {
        foreach (var (stats, index) in _stats.Select((s, i) => (s, i)))
        {
            var duration = $"{(int)stats.Value.Duration.TotalHours:00}:{stats.Value.Duration.Minutes:00}:{stats.Value.Duration.Seconds:00}";
            
            table.UpdateCell(index, 1, duration);
            table.UpdateCell(index, 2, GetStatusMarkup(stats.Value.Status));
        }
    }

    private void UpdateStatsTable(Table table)
    {
        int GetRowIndex(int tableRow, int index) => index * 3 + tableRow;

        foreach (var (stats, index) in _stats.Select((s, i) => (s, i)))
        {
            table.UpdateCell(GetRowIndex(0, index), 2, $"{stats.Value.ScenarioLoadResultSnapshot.RequestCount}");
            
            table.UpdateCell(GetRowIndex(1, index), 2, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.RequestCount}");
            table.UpdateCell(GetRowIndex(1, index), 3, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.RequestsPerSecond}");
            table.UpdateCell(GetRowIndex(1, index), 4, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.ResponseTimeMin.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(1, index), 5, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.ResponseTimeMean.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(1, index), 6, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.ResponseTimeMax.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(1, index), 7, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(1, index), 8, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.ResponseTimeMedian.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(1, index), 9, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.ResponseTimePercentile75.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(1, index), 10, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.ResponseTimePercentile95.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(1, index), 11, $"{stats.Value.ScenarioLoadResultSnapshot.Ok.ResponseTimePercentile99.ToTestFuznResponseTime()}");

            table.UpdateCell(GetRowIndex(2, index), 2, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.RequestCount}");
            table.UpdateCell(GetRowIndex(2, index), 3, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.RequestsPerSecond}");
            table.UpdateCell(GetRowIndex(2, index), 4, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.ResponseTimeMin.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(2, index), 5, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.ResponseTimeMean.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(2, index), 6, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.ResponseTimeMax.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(2, index), 7, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(2, index), 8, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.ResponseTimeMedian.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(2, index), 9, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.ResponseTimePercentile75.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(2, index), 10, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.ResponseTimePercentile95.ToTestFuznResponseTime()}");
            table.UpdateCell(GetRowIndex(2, index), 11, $"{stats.Value.ScenarioLoadResultSnapshot.Failed.ResponseTimePercentile99.ToTestFuznResponseTime()}");
        }
    }

    private Table BuildStepsTable()
    {
        var table = new Table { Border = TableBorder.Minimal };
        table.AddColumn("[u]Step[/]");
        table.AddColumn("[u]Total[/]");
        table.AddColumn("[u]Ok[/]");
        table.AddColumn("[u]Failed[/]");

        foreach (var scenarioStats in _stats)
        {
            foreach (var step in scenarioStats.Value.ScenarioLoadResultSnapshot.Steps)
                table.AddRow(
                    $"{step.Key}",
                    $"{step.Value.RequestCount}",
                    $"{step.Value.Ok.RequestCount}",
                    $"{step.Value.Failed.RequestCount}"
                );
        }

        return table;
    }

    private void UpdateStepsTables(Table stepTable)
    {
        try
        {
            foreach (var scenarioStats in _stats)
            {
                foreach (var (step, index) in scenarioStats.Value.ScenarioLoadResultSnapshot.Steps.Select((s, i) => (s, i)))
                {
                    stepTable.UpdateCell(index, 1, $"{step.Value.RequestCount}");
                    stepTable.UpdateCell(index, 2, $"{step.Value.Ok.RequestCount}");
                    stepTable.UpdateCell(index, 3, $"{step.Value.Failed.RequestCount}");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private string GetStatusMarkup(string status)
    {
        return status switch
        {
            "Passed" => "[green]Passed[/]",
            "Failed" => "[red]Failed[/]",
            _ => "[yellow]Running[/]"
        };
    }
}

internal class LiveMetrics
{
    public string Status { get; set; }
    public TimeSpan Duration { get; set; }
    public bool ConsoleCompleted { get; set; }
    public ScenarioLoadResult ScenarioLoadResultSnapshot { get; set; }
}
