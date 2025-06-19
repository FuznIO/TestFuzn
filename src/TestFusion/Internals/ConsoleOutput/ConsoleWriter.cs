using System.Text;
using TestFusion.ConsoleOutput;
using TestFusion.Internals.ConsoleOutput;
using TestFusion.Internals.Results.Load;
using TestFusion.Internals.State;
using TestFusion.Plugins.TestFrameworkProviders;
using TestFusion.Results.Feature;
using TestFusion.Results.Load;

namespace TestFusion.Internals.ConsoleOutput;

internal class ConsoleWriter
{
    private readonly ITestFrameworkProvider _testFramework;
    private readonly SharedExecutionState _sharedExecutionState;
    private readonly LoadResultsManager _loadResultsManager;
    private CursorPosition _cursorPosition;

    public ConsoleWriter(ITestFrameworkProvider testFramework,
        SharedExecutionState sharedExecutionState,
        LoadResultsManager loadResultsManager)
    {
        _testFramework = testFramework;
        _sharedExecutionState = sharedExecutionState;
        _loadResultsManager = loadResultsManager;
    }

    public void ScenarioStart()
    {
        if (_sharedExecutionState.TestType == Internals.TestType.Load)
            return;

        //TODO: medium pri, implementer for featuretest testfusionprovider live view
        //var scenario = _sharedExecutionState.Scenarios.Single();
        //_testFramework.WritePanel([$"[bold yellow]{scenario.Name}[/]"], "[bold]Scenario Start[/]");
    }

    public void IterationExecutionStart(IterationResult iterationResult)
    {
        if (_sharedExecutionState.TestType == Internals.TestType.Load || !_testFramework.SupportsRealTimeConsoleOutput)
            return;

        //TODO: medium pri, implementer for featuretest testfusionprovider live view
        //_testFramework.WriteMarkup($"[blue]➡️ {PropertyHelper.GetStringFromProperties(iterationResult.InputData)} - Executing[/]");
    }

    public void StepExecutionStart(int stepIndex, int totalSteps, BaseStep step)
    {
        if (_sharedExecutionState.TestType == Internals.TestType.Load || !_testFramework.SupportsRealTimeConsoleOutput)
            return;

        //TODO: medium pri, implementer for featuretest testfusionprovider live view
        //_testFramework.WriteMarkup($"[yellow]🔄 Step {stepIndex}/{totalSteps}: [bold]{step.Name}[/] - Executing[/]");
    }

    public void StepExecutionEnd(int stepIndex, int totalSteps, StepResult stepResult)
    {
        if (_sharedExecutionState.TestType == Internals.TestType.Load)
            return;

        //TODO: medium pri, implementer for featuretest testfusionprovider live view
        //string duration = stepResult.Duration.ToTestFusionResponseTime();
        //switch (stepResult.Status)
        //{
        //    case StepStatus.Passed:
        //        _testFramework.WriteMarkup($"[green]✅ Step {stepIndex}/{totalSteps}: {stepResult.Name} - Passed ({duration})[/]");
        //        break;
        //    case StepStatus.Failed:
        //        _testFramework.WriteMarkup($"[red]❌ Step {stepIndex}/{totalSteps}: {stepResult.Name} - Failed ({duration})[/]");
        //        if (stepResult.Exception != null)
        //        {
        //            _testFramework.WriteMarkup($"[red]   Message: {stepResult.Exception.Message}[/]");
        //            _testFramework.WriteMarkup($"[grey]   Callstack: {stepResult.Exception.StackTrace.Replace("[","<").Replace("]",">")}[/]");
        //        }
        //        break;
        //    case StepStatus.Skipped:
        //        _testFramework.WriteMarkup($"[yellow]⏭️ Step {stepIndex}/{totalSteps}: {stepResult.Name} - Skipped - Previous step failed[/]");
        //        break;
        //    default:
        //        throw new NotImplementedException("StepStatus is not implemented");
        //}
    }

    public void IterationExecutionEnd(StepContext context, IterationResult iterationResult)
    {
        if (_sharedExecutionState.TestType == Internals.TestType.Load)
            return;

        //TODO: medium pri, implementer for featuretest testfusionprovider live view
        //if (context.InputDataInternal != null)
        //{
        //    var duration = iterationResult.Duration.ToTestFusionResponseTime();
        //    if (iterationResult.StepResults.All(x => x.Value.Status == StepStatus.Passed))
        //        _testFramework.WriteMarkup($"[green]✅ Input {iterationResult.InputData} - Passed ({duration})[/]\n");
        //    else
        //        _testFramework.WriteMarkup($"[red]❌ Input {iterationResult.InputData} - Failed ({duration})[/]\n");
        //}
    }

    public void WriteSummary()
    {
        if (_sharedExecutionState.TestType == Internals.TestType.Feature)
            WriteSummaryFeature();
        else
            WriteSummaryLoad();
    }

    public void WriteSummaryFeature()
    {
        var scenario = _sharedExecutionState.Scenarios.Single();
        var scenarioResult = _sharedExecutionState.ScenarioResult;
        
        if (scenarioResult.IterationResults.Count > 0)
        {
            var table = new AdvancedTable
            {
                ColumnCount = 5,
                ColumnWidths = new List<int> { 26, 60 }
            };

            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell($"Scenario: {scenario.Name}", 4),
                    new AdvancedTableCell($"Duration", 1)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell($"", 4),
                    new AdvancedTableCell($"{(scenarioResult.EndTime - scenarioResult.StartTime).ToTestFusionFormattedDuration()}", 1)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell($"Status: {scenarioResult.Status}", 5)
                }
            });
            table.Rows.Add(new AdvancedTableRow { IsDivider = true });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell($"Step Name", 5)
                }
            });
            table.Rows.Add(new AdvancedTableRow { IsDivider = true });
            foreach (var (iterationResult, iterationIndex) in scenarioResult.IterationResults.Select((ir, i) => (ir, i)))
            {
                if (iterationResult.InputData != null)
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell("Input data", 1),
                            new AdvancedTableCell($"{(iterationResult.InputData?.Length > 50 ? iterationResult.InputData[..50] + "..." : iterationResult.InputData)}", 4),
                        }
                    });

                foreach (var (stepResult, index) in iterationResult.StepResults.Select((sr, i) => (sr, i)))
                {
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                    {
                        new AdvancedTableCell($"Step {index + 1}/{iterationResult.StepResults.Count}: {stepResult.Key}", 4),
                        new KeyValueCell($"{stepResult.Value.Status}", $"{stepResult.Value.Duration.ToTestFusionResponseTime()}", 1)
                    }
                    });

                    if (iterationResult.InputData != null && index + 1 == iterationResult.StepResults.Count && iterationIndex + 1 < scenarioResult.IterationResults.Count)
                        table.Rows.Add(new AdvancedTableRow { IsDivider = true });
                }
            }

            string GetStatusSymbol(StepStatus status)
            {
                return status switch
                {
                    StepStatus.Passed => "✅",
                    StepStatus.Failed => "❌",
                    StepStatus.Skipped => "⏭️",
                    _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
                };
            }
            _testFramework.Write(Environment.NewLine);
            _testFramework.WriteAdvancedTable(table);

            // Errors
            // TODO LEK
            var errorSection = new StringBuilder();
            //foreach (var step in scenario.Steps)
            //{
            //    var stepResult = loadResult.Steps[step.Name];
            //    if (stepResult.Errors?.Count > 0)
            //    {
            //        if (errorSection.Length == 0)
            //            errorSection.AppendLine("[red]Errors by Step:[/]");
            //        errorSection.AppendLine($"[red]{step.Name}:[/]");
            //        foreach (var error in stepResult.Errors)
            //            errorSection.AppendLine($"  [red]{error.Key} (Count: {error.Value.Count})[/]");
            //    }
            //}
            if (errorSection.Length > 0)
            {
                _testFramework.Write(Environment.NewLine);
                _testFramework.WriteMarkup(errorSection.ToString());
            }
        }
    }

    private void WriteSummaryLoad()
    {
        if (_cursorPosition == null)
            _cursorPosition = _testFramework.GetCursorPosition();

        if (_testFramework.SupportsRealTimeConsoleOutput)
        {
            var loadtestResults = new Dictionary<Scenario, ScenarioLoadResult>();

            foreach (var scenario in _sharedExecutionState.Scenarios)
            {
                loadtestResults.TryAdd(scenario, _loadResultsManager.GetScenarioCollector(scenario.Name).GetCurrentResult());
            }
            
            _testFramework.WriteSummary(loadtestResults.First().Value.StartTime, _loadResultsManager.TotalRunDuration, loadtestResults);
            return;
        }

        var elapsed = _loadResultsManager.TotalRunDuration.ToString(@"hh\:mm\:ss\:ff");

        _testFramework.WriteMarkup($"[bold]Total elapsed Time:[/] [yellow]{elapsed}[/]");
        if (_sharedExecutionState.IsConsumingCompleted || _sharedExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
        {
            if (_sharedExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
                _testFramework.WriteMarkup($"[red]Status: Stopped, reason: {_sharedExecutionState.ExecutionStoppedReason.Message}[/]\r\n");
            else
                _testFramework.WriteMarkup("[green]Status: ConsoleCompleted successfully.[/]\r\n");
        }

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            var loadResult = _loadResultsManager.GetScenarioCollector(scenario.Name).GetCurrentResult();

            var table = new AdvancedTable
            {
                ColumnCount = 5,
                ColumnWidths = new List<int> { 26, 60 }
            };

            table.Rows.Add(new AdvancedTableRow {
                Cells =
                {
                    new AdvancedTableCell($"Scenario: {scenario.Name}", 3),
                    new AdvancedTableCell($"Execution Time", 1),
                    new AdvancedTableCell($"Test Run Time", 1)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell($"", 3),
                    new AdvancedTableCell($"{loadResult.TotalExecutionDuration.ToTestFusionFormattedDuration()}", 1),
                    new AdvancedTableCell($"{(loadResult.EndTime - loadResult.StartTime).ToTestFusionFormattedDuration()}", 1)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell($"Status: {loadResult.Status}", 5)
                }
            });
            table.Rows.Add(new AdvancedTableRow { IsDivider = true });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Load Simulations", 1),
                    new AdvancedTableCell($"{scenario.SimulationsInternal.First().GetDescription()}", 4)
                }
            });
            if (scenario.SimulationsInternal.Count > 1)
            {
                table.Rows.AddRange(scenario.SimulationsInternal.Skip(1).Select(s => new AdvancedTableRow
                {
                    Cells = [
                        new AdvancedTableCell(string.Empty, 1),
                        new AdvancedTableCell(s.GetDescription(), 4)
                    ]
                }));
            }
            table.Rows.Add(new AdvancedTableRow { IsDivider = true });

            
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Total Requests", 1),
                    new KeyValueCell("Count:", $"{loadResult.RequestCount}", 1),
                    new AdvancedTableCell(string.Empty, 3)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Requests - Ok", 1),
                    new KeyValueCell("Count:", $"{loadResult.Ok.RequestCount}", 1),
                    new KeyValueCell("RPS:", $"{loadResult.Ok.RequestsPerSecond}", 1),
                    new AdvancedTableCell(string.Empty, 2)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Requests - Failed", 1),
                    new KeyValueCell("Count:", $"{loadResult.Failed.RequestCount}", 1),
                    new KeyValueCell("RPS:", $"{(loadResult.Failed.RequestCount > 0 ? loadResult.Failed.RequestsPerSecond : "N/A")}", 1),
                    new AdvancedTableCell(string.Empty, 2)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Response Time - Ok", 1),
                    new KeyValueCell("Min:", $"{loadResult.Ok.ResponseTimeMin.ToTestFusionResponseTime()}", 1),
                    new KeyValueCell("Mean:", $"{loadResult.Ok.ResponseTimeMean.ToTestFusionResponseTime()}", 1),
                    new KeyValueCell("Max:", $"{loadResult.Ok.ResponseTimeMax.ToTestFusionResponseTime()}", 1),
                    new KeyValueCell("StdDev:", $"{loadResult.Ok.ResponseTimeStandardDeviation.ToTestFusionResponseTime()}", 1)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell(string.Empty, 1),
                    new KeyValueCell("Median:", $"{loadResult.Ok.ResponseTimeMedian.ToTestFusionResponseTime()}", 1),
                    new KeyValueCell("P75:", $"{loadResult.Ok.ResponseTimePercentile75.ToTestFusionResponseTime()}", 1),
                    new KeyValueCell("P95:", $"{loadResult.Ok.ResponseTimePercentile95.ToTestFusionResponseTime()}", 1),
                    new KeyValueCell("P99:", $"{loadResult.Ok.ResponseTimePercentile99.ToTestFusionResponseTime()}", 1)
                }
            });
            if (loadResult.Failed.RequestCount > 0)
            {
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Response Time - Failed", 1),
                        new KeyValueCell("Min:", $"{loadResult.Failed.ResponseTimeMin.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("Mean:", $"{loadResult.Failed.ResponseTimeMean.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("Max:", $"{loadResult.Failed.ResponseTimeMax.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("StdDev:", $"{loadResult.Failed.ResponseTimeStandardDeviation.ToTestFusionResponseTime()}", 1)
                    }
                });

                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(string.Empty, 1),
                        new KeyValueCell("Median:", $"{loadResult.Failed.ResponseTimeMedian.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("P75:", $"{loadResult.Failed.ResponseTimePercentile75.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("P95:", $"{loadResult.Failed.ResponseTimePercentile95.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("P99:", $"{loadResult.Failed.ResponseTimePercentile99.ToTestFusionResponseTime()}", 1)
                    }
                });
            }

            foreach (var step in scenario.Steps)
            {
                var stepResult = loadResult.Steps[step.Name];
                table.Rows.Add(new AdvancedTableRow { IsDivider = true });
                table.Rows.Add(new AdvancedTableRow { Cells =
                {
                    new AdvancedTableCell($"Step Name", 1),
                    new AdvancedTableCell($"{step.Name}", 4)
                } });
                table.Rows.Add(new AdvancedTableRow { Cells =
                {
                    new AdvancedTableCell("Total Requests", 1),
                    new KeyValueCell("Count:", $"{stepResult.RequestCount}", 1),
                    new AdvancedTableCell(string.Empty, 3)
                } });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Requests - Ok", 1),
                        new KeyValueCell("Count:", $"{stepResult.Ok.RequestCount}", 1),
                        new KeyValueCell("RPS:", $"{stepResult.Ok.RequestsPerSecond}", 1),
                        new AdvancedTableCell(string.Empty, 2)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Requests - Failed", 1),
                        new KeyValueCell("Count:", $"{stepResult.Failed.RequestCount}", 1),
                        new KeyValueCell("RPS:", $"{(stepResult.Failed.RequestCount > 0 ? stepResult.Failed.RequestsPerSecond : "N/A")}", 1),
                        new AdvancedTableCell(string.Empty, 2)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Response Time - Ok", 1),
                        new KeyValueCell("Min:", $"{stepResult.Ok.ResponseTimeMin.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("Mean:", $"{stepResult.Ok.ResponseTimeMean.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("Max:", $"{stepResult.Ok.ResponseTimeMax.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("StdDev:", $"{stepResult.Ok.ResponseTimeStandardDeviation.ToTestFusionResponseTime()}", 1)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(string.Empty, 1),
                        new KeyValueCell("Median:", $"{stepResult.Ok.ResponseTimeMedian.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("P75:", $"{stepResult.Ok.ResponseTimePercentile75.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("P95:", $"{stepResult.Ok.ResponseTimePercentile95.ToTestFusionResponseTime()}", 1),
                        new KeyValueCell("P99:", $"{stepResult.Ok.ResponseTimePercentile99.ToTestFusionResponseTime()}", 1)
                    }
                });
                if (stepResult.Failed.RequestCount > 0)
                {
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell("Response Time - Failed", 1),
                            new KeyValueCell("Min:", $"{stepResult.Failed.ResponseTimeMin.ToTestFusionResponseTime()}", 1),
                            new KeyValueCell("Mean:", $"{stepResult.Failed.ResponseTimeMean.ToTestFusionResponseTime()}", 1),
                            new KeyValueCell("Max:", $"{stepResult.Failed.ResponseTimeMax.ToTestFusionResponseTime()}", 1),
                            new KeyValueCell("StdDev:", $"{stepResult.Failed.ResponseTimeStandardDeviation.ToTestFusionResponseTime()}", 1)
                        }
                    });
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell(string.Empty, 1),
                            new KeyValueCell("Median:", $"{stepResult.Failed.ResponseTimeMedian.ToTestFusionResponseTime()}", 1),
                            new KeyValueCell("P75:", $"{stepResult.Failed.ResponseTimePercentile75.ToTestFusionResponseTime()}", 1),
                            new KeyValueCell("P95:", $"{stepResult.Failed.ResponseTimePercentile95.ToTestFusionResponseTime()}", 1),
                            new KeyValueCell("P99:", $"{stepResult.Failed.ResponseTimePercentile99.ToTestFusionResponseTime()}", 1)
                        }
                    });
                }
            }

            _testFramework.WriteAdvancedTable(table);

            //Assertion
            var assertionSection = new StringBuilder();
            if (loadResult.AssertWhenDoneExceptions != null)
            {
                foreach (var exception in loadResult.AssertWhenDoneExceptions)
                {
                    if (assertionSection.Length == 0)
                        assertionSection.AppendLine("[red]Assert exceptions:[/]");

                    assertionSection.AppendLine($"  [red]{exception.Message}[/]");
                }
            }
            if (assertionSection.Length > 0)
            {
                _testFramework.Write(Environment.NewLine);
                _testFramework.WriteMarkup(assertionSection.ToString());
            }

            // Errors
            var errorSection = new StringBuilder();
            foreach (var step in scenario.Steps)
            {
                var stepResult = loadResult.Steps[step.Name];
                if (stepResult.Errors?.Count > 0)
                {
                    if (errorSection.Length == 0)
                        errorSection.AppendLine("[red]Errors by Step:[/]");
                    errorSection.AppendLine($"[red]{step.Name}:[/]");
                    foreach (var error in stepResult.Errors)
                        errorSection.AppendLine($"  [red]{error.Key} (Count: {error.Value.Count})[/]");
                }
            }
            if (errorSection.Length > 0)
            {
                _testFramework.Write(Environment.NewLine);
                _testFramework.WriteMarkup(errorSection.ToString());
            }
        }
    }
}
