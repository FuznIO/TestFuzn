using System.Text;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Internals.Results.Load;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn.Internals.ConsoleOutput;

internal class ConsoleWriter
{
    private readonly ITestFrameworkAdapter _testFramework;
    private readonly TestExecutionState _testExecutionState;
    private CursorPosition _cursorPosition;

    public ConsoleWriter(ITestFrameworkAdapter testFramework,
        TestExecutionState testExecutionState)
    {
        _testFramework = testFramework;
        _testExecutionState = testExecutionState;
    }

    public void WriteSummary()
    {
        if (_testExecutionState.TestResult.TestType == TestType.Standard)
            WriteSummaryStandard();
        else
            WriteSummaryLoad();
    }

    public void WriteSummaryStandard()
    {
        var scenario = _testExecutionState.Scenarios.Single();
        var scenarioResult = _testExecutionState.TestResult;
        
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
                    new AdvancedTableCell($"{scenarioResult.TestRunTotalDuration().ToTestFuznFormattedDuration()}", 1)
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
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("CorrelationId", 1),
                        new AdvancedTableCell($"{iterationResult.CorrelationId}", 4),
                    }
                });

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
                    var stepNumber = index + 1;
                    var stepDisplayName = $"Step {stepNumber}: {stepResult.Key}";

                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell(stepDisplayName, 4),
                            new KeyValueCell($"{stepResult.Value.Status}", $"{stepResult.Value.Duration.ToTestFuznResponseTime()}", 1)
                        }
                    });

                    AddCommentsAndAttachmentsRows(table, stepResult.Value, "  ");

                    var subStepResults = SubStepHelper.GetSubStepResults(stepResult.Value, [stepNumber]);
                    foreach (var sub in subStepResults)
                    {
                        var indent = new string(' ', sub.Level * 2 - 2);
                        var subStepDisplayName = $"{indent}↳ Step {sub.StepNumber}: {sub.Name}";
                        
                        table.Rows.Add(new AdvancedTableRow
                        {
                            Cells =
                            {
                                new AdvancedTableCell(subStepDisplayName, 4),
                                new KeyValueCell($"{sub.Status}", $"{sub.Duration.ToTestFuznResponseTime()}", 1)
                            }
                        });

                        AddCommentsAndAttachmentsRows(table, sub, $"{indent}  ");
                    }

                    if (iterationResult.InputData != null && stepNumber == iterationResult.StepResults.Count && iterationIndex + 1 < scenarioResult.IterationResults.Count)
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

            var errorSection = new StringBuilder();

            if (errorSection.Length > 0)
            {
                _testFramework.Write(Environment.NewLine);
                _testFramework.WriteMarkup(errorSection.ToString());
            }
        }
    }

    private static void AddCommentsAndAttachmentsRows(AdvancedTable table, StepStandardResult stepResult, string indent)
    {
        if (stepResult.Comments is { Count: > 0 })
        {
            foreach (var comment in stepResult.Comments)
            {
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell($"{indent}// {comment.Text}", 5)
                    }
                });
            }
        }

        if (stepResult.Attachments is { Count: > 0 })
        {
            foreach (var attachment in stepResult.Attachments)
            {
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell($"{indent}📎 {attachment.Path}", 5)
                    }
                });
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

            foreach (var scenario in _testExecutionState.Scenarios)
            {
                loadtestResults.TryAdd(scenario, _testExecutionState.LoadCollectors[scenario.Name].GetCurrentResult());
            }
            
            _testFramework.WriteSummary(loadtestResults.First().Value.StartTime(), _testExecutionState.TestRunState.TestRunDuration(), loadtestResults);
            return;
        }

        var elapsed = _testExecutionState.TestRunState.TestRunDuration().ToString(@"hh\:mm\:ss\:ff");

        _testFramework.WriteMarkup($"[bold]Total elapsed Time:[/] [yellow]{elapsed}[/]");
        if (_testExecutionState.IsConsumingCompleted || _testExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Stopped)
        {
            if (_testExecutionState.TestRunState.ExecutionStatus == ExecutionStatus.Stopped)
                _testFramework.WriteMarkup($"[red]Status: Stopped, reason: {_testExecutionState.TestRunState.ExecutionStoppedReason.Message}[/]\r\n");
            else
                _testFramework.WriteMarkup("[green]Status: Completed successfully.[/]\r\n");
        }

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            var loadResult = _testExecutionState.LoadCollectors[scenario.Name].GetCurrentResult();

            var table = new AdvancedTable
            {
                ColumnCount = 5,
                ColumnWidths = new List<int> { 26, 60 }
            };

            table.Rows.Add(new AdvancedTableRow {
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
                    new AdvancedTableCell($"{loadResult.TestRunTotalDuration().ToTestFuznFormattedDuration()}", 1),
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
                    new KeyValueCell("Min:", $"{loadResult.Ok.ResponseTimeMin.ToTestFuznResponseTime()}", 1),
                    new KeyValueCell("Mean:", $"{loadResult.Ok.ResponseTimeMean.ToTestFuznResponseTime()}", 1),
                    new KeyValueCell("Max:", $"{loadResult.Ok.ResponseTimeMax.ToTestFuznResponseTime()}", 1),
                    new KeyValueCell("StdDev:", $"{loadResult.Ok.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}", 1)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell(string.Empty, 1),
                    new KeyValueCell("Median:", $"{loadResult.Ok.ResponseTimeMedian.ToTestFuznResponseTime()}", 1),
                    new KeyValueCell("P75:", $"{loadResult.Ok.ResponseTimePercentile75.ToTestFuznResponseTime()}", 1),
                    new KeyValueCell("P95:", $"{loadResult.Ok.ResponseTimePercentile95.ToTestFuznResponseTime()}", 1),
                    new KeyValueCell("P99:", $"{loadResult.Ok.ResponseTimePercentile99.ToTestFuznResponseTime()}", 1)
                }
            });
            if (loadResult.Failed.RequestCount > 0)
            {
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Response Time - Failed", 1),
                        new KeyValueCell("Min:", $"{loadResult.Failed.ResponseTimeMin.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("Mean:", $"{loadResult.Failed.ResponseTimeMean.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("Max:", $"{loadResult.Failed.ResponseTimeMax.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("StdDev:", $"{loadResult.Failed.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}", 1)
                    }
                });

                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(string.Empty, 1),
                        new KeyValueCell("Median:", $"{loadResult.Failed.ResponseTimeMedian.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("P75:", $"{loadResult.Failed.ResponseTimePercentile75.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("P95:", $"{loadResult.Failed.ResponseTimePercentile95.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("P99:", $"{loadResult.Failed.ResponseTimePercentile99.ToTestFuznResponseTime()}", 1)
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
                        new KeyValueCell("Min:", $"{stepResult.Ok.ResponseTimeMin.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("Mean:", $"{stepResult.Ok.ResponseTimeMean.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("Max:", $"{stepResult.Ok.ResponseTimeMax.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("StdDev:", $"{stepResult.Ok.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}", 1)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(string.Empty, 1),
                        new KeyValueCell("Median:", $"{stepResult.Ok.ResponseTimeMedian.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("P75:", $"{stepResult.Ok.ResponseTimePercentile75.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("P95:", $"{stepResult.Ok.ResponseTimePercentile95.ToTestFuznResponseTime()}", 1),
                        new KeyValueCell("P99:", $"{stepResult.Ok.ResponseTimePercentile99.ToTestFuznResponseTime()}", 1)
                    }
                });
                if (stepResult.Failed.RequestCount > 0)
                {
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell("Response Time - Failed", 1),
                            new KeyValueCell("Min:", $"{stepResult.Failed.ResponseTimeMin.ToTestFuznResponseTime()}", 1),
                            new KeyValueCell("Mean:", $"{stepResult.Failed.ResponseTimeMean.ToTestFuznResponseTime()}", 1),
                            new KeyValueCell("Max:", $"{stepResult.Failed.ResponseTimeMax.ToTestFuznResponseTime()}", 1),
                            new KeyValueCell("StdDev:", $"{stepResult.Failed.ResponseTimeStandardDeviation.ToTestFuznResponseTime()}", 1)
                        }
                    });
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell(string.Empty, 1),
                            new KeyValueCell("Median:", $"{stepResult.Failed.ResponseTimeMedian.ToTestFuznResponseTime()}", 1),
                            new KeyValueCell("P75:", $"{stepResult.Failed.ResponseTimePercentile75.ToTestFuznResponseTime()}", 1),
                            new KeyValueCell("P95:", $"{stepResult.Failed.ResponseTimePercentile95.ToTestFuznResponseTime()}", 1),
                            new KeyValueCell("P99:", $"{stepResult.Failed.ResponseTimePercentile99.ToTestFuznResponseTime()}", 1)
                        }
                    });
                }
            }

            _testFramework.WriteAdvancedTable(table);

            //Assertion
            var assertionSection = new StringBuilder();

            if (loadResult.AssertWhileRunningException != null)
            {
                assertionSection.AppendLine("[red]Assert exceptions:[/]");

                assertionSection.AppendLine($"  [red]{loadResult.AssertWhileRunningException.Message}[/]");
            }

            if (loadResult.AssertWhenDoneException != null)
            {
                if (assertionSection.Length == 0)
                    assertionSection.AppendLine("[red]Assert exceptions:[/]");

                assertionSection.AppendLine($"  [red]{loadResult.AssertWhenDoneException.Message}[/]");
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
