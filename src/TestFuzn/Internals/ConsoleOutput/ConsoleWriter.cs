using System.Text;
using Fuzn.TestFuzn.ConsoleOutput;
using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn.Internals.ConsoleOutput;

internal class ConsoleWriter
{
    private TestExecutionState _testExecutionState = null!;

    public void WriteSummary(TestExecutionState testExecutionState)
    {
        _testExecutionState = testExecutionState;

        if (_testExecutionState.TestResult.TestType == TestType.Standard)
            WriteSummaryStandard();
        else
            WriteSummaryLoad();
    }

    private void WriteSummaryStandard()
    {
        var testFramework = _testExecutionState.TestFramework;
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
                    new AdvancedTableCell($"{scenarioResult.TestRunDuration().ToTestFuznFormattedDuration()}", 1)
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
                {
                    var inputText = iterationResult.InputData.ToString() ?? string.Empty;
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell("Input data", 1),
                            new AdvancedTableCell(inputText.Length > 50 ? inputText[..50] + "..." : inputText, 4),
                        }
                    });
                }

                foreach (var (stepResult, index) in iterationResult.StepResults.Select((sr, i) => (sr, i)))
                {
                    var stepNumber = index + 1;
                    var stepDisplayName = $"Step {stepNumber}: {stepResult.Key}";

                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell(stepDisplayName, 4),
                            new KeyValueCell($"{stepResult.Value.Status}", $"{stepResult.Value.Duration.ToTestFuznDuration()}", 1)
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
                                new KeyValueCell($"{sub.Status}", $"{sub.Duration.ToTestFuznDuration()}", 1)
                            }
                        });

                        AddCommentsAndAttachmentsRows(table, sub, $"{indent}  ");
                    }

                    if (iterationResult.InputData != null && stepNumber == iterationResult.StepResults.Count && iterationIndex + 1 < scenarioResult.IterationResults.Count)
                        table.Rows.Add(new AdvancedTableRow { IsDivider = true });
                }
            }
            testFramework.Write(Environment.NewLine);
            testFramework.WriteAdvancedTable(table);

            WriteReportLink(testFramework);
        }
    }

    private void WriteReportLink(ITestFrameworkAdapter testFramework)
    {
        testFramework.Write(Environment.NewLine);
        testFramework.Write(ReportLinkText());
        testFramework.Write(Environment.NewLine);
    }

    private string ReportLinkText()
    {
        return $"Report: {ReportPath()}";
    }

    private string ExecutionEnvironment()
    {
        var configuration = _testExecutionState.TestSession.Configuration;
        if (configuration == null)
            return string.Empty;

        return TextOrEmpty(configuration.ExecutionEnvironment);
    }

    private string TargetEnvironment()
    {
        var configuration = _testExecutionState.TestSession.Configuration;
        if (configuration == null)
            return string.Empty;

        return TextOrEmpty(configuration.TargetEnvironment);
    }

    private static string TextOrEmpty(string? value)
    {
        if (value == null)
            return string.Empty;

        return value;
    }

    private string ReportPath()
    {
        var remoteRunUrl = _testExecutionState.TestSession.Cluster.RemoteRunUrl;
        if (remoteRunUrl != null)
            return remoteRunUrl;

        return Path.GetFullPath(Path.Combine(_testExecutionState.TestSession.TestsResultsDirectory, "TestReport.html"));
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
        var testFramework = _testExecutionState.TestFramework;

        if (testFramework.SupportsRealTimeConsoleOutput)
        {
            var loadtestResults = new Dictionary<Scenario, ScenarioLoadResult>();

            foreach (var scenario in _testExecutionState.Scenarios)
            {
                loadtestResults.TryAdd(scenario, _testExecutionState.LoadCollectors[scenario.Name].GetCurrentResult());
            }
            
            testFramework.WriteSummary(loadtestResults.First().Value.StartTime(), _testExecutionState.TestRunDuration(), loadtestResults, ReportPath(), ExecutionEnvironment(), TargetEnvironment());
            return;
        }

        var elapsed = _testExecutionState.TestRunDuration().ToString(@"hh\:mm\:ss\:ff");

        testFramework.WriteMarkup($"[bold]Total elapsed Time:[/] [yellow]{elapsed}[/]");
        if (_testExecutionState.IsConsumingCompleted || _testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
        {
            if (_testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
            {
                var stoppedReason = _testExecutionState.ExecutionStoppedReason;
                if (stoppedReason == null)
                    testFramework.WriteMarkup("[red]Status: Stopped[/]\r\n");
                else
                    testFramework.WriteMarkup($"[red]Status: Stopped, reason: {stoppedReason.Message}[/]\r\n");
            }
            else
            {
                testFramework.WriteMarkup("[green]Status: Completed successfully.[/]\r\n");
            }
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
            // A cluster run that failed before the workers sent their simulations has none.
            var simulationDescriptions = scenario.SimulationsInternal.Count > 0
                ? scenario.SimulationsInternal.Select(s => s.GetDescription()).ToList()
                : ["-"];
            for (var index = 0; index < simulationDescriptions.Count; index++)
            {
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(index == 0 ? "Load Simulations" : string.Empty, 1),
                        new AdvancedTableCell(simulationDescriptions[index], 4)
                    }
                });
            }
            table.Rows.Add(new AdvancedTableRow { IsDivider = true });

            
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Total Iterations", 1),
                    new KeyValueCell("Count:", $"{loadResult.Count}", 1),
                    new AdvancedTableCell(string.Empty, 3)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Iterations - Ok", 1),
                    new KeyValueCell("Count:", $"{loadResult.Ok.Count}", 1),
                    new KeyValueCell("Per Second:", $"{loadResult.Ok.PerSecond}", 1),
                    new AdvancedTableCell(string.Empty, 2)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Iterations - Failed", 1),
                    new KeyValueCell("Count:", $"{loadResult.Failed.Count}", 1),
                    new KeyValueCell("Per Second:", $"{(loadResult.Failed.Count > 0 ? loadResult.Failed.PerSecond : "N/A")}", 1),
                    new AdvancedTableCell(string.Empty, 2)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell("Response Time - Ok", 1),
                    new KeyValueCell("Min:", $"{loadResult.Ok.DurationMin.ToTestFuznDuration()}", 1),
                    new KeyValueCell("Mean:", $"{loadResult.Ok.DurationMean.ToTestFuznDuration()}", 1),
                    new KeyValueCell("Max:", $"{loadResult.Ok.DurationMax.ToTestFuznDuration()}", 1),
                    new KeyValueCell("StdDev:", $"{loadResult.Ok.DurationStandardDeviation.ToTestFuznDuration()}", 1)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell(string.Empty, 1),
                    new KeyValueCell("Median:", $"{loadResult.Ok.DurationMedian.ToTestFuznDuration()}", 1),
                    new KeyValueCell("P75:", $"{loadResult.Ok.DurationPercentile75.ToTestFuznDuration()}", 1),
                    new KeyValueCell("P90:", $"{loadResult.Ok.DurationPercentile90.ToTestFuznDuration()}", 1),
                    new KeyValueCell("P95:", $"{loadResult.Ok.DurationPercentile95.ToTestFuznDuration()}", 1)
                }
            });
            table.Rows.Add(new AdvancedTableRow
            {
                Cells =
                {
                    new AdvancedTableCell(string.Empty, 1),
                    new KeyValueCell("P99:", $"{loadResult.Ok.DurationPercentile99.ToTestFuznDuration()}", 1),
                    new AdvancedTableCell(string.Empty, 3)
                }
            });
            if (loadResult.Failed.Count > 0)
            {
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Response Time - Failed", 1),
                        new KeyValueCell("Min:", $"{loadResult.Failed.DurationMin.ToTestFuznDuration()}", 1),
                        new KeyValueCell("Mean:", $"{loadResult.Failed.DurationMean.ToTestFuznDuration()}", 1),
                        new KeyValueCell("Max:", $"{loadResult.Failed.DurationMax.ToTestFuznDuration()}", 1),
                        new KeyValueCell("StdDev:", $"{loadResult.Failed.DurationStandardDeviation.ToTestFuznDuration()}", 1)
                    }
                });

                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(string.Empty, 1),
                        new KeyValueCell("Median:", $"{loadResult.Failed.DurationMedian.ToTestFuznDuration()}", 1),
                        new KeyValueCell("P75:", $"{loadResult.Failed.DurationPercentile75.ToTestFuznDuration()}", 1),
                        new KeyValueCell("P90:", $"{loadResult.Failed.DurationPercentile90.ToTestFuznDuration()}", 1),
                        new KeyValueCell("P95:", $"{loadResult.Failed.DurationPercentile95.ToTestFuznDuration()}", 1)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(string.Empty, 1),
                        new KeyValueCell("P99:", $"{loadResult.Failed.DurationPercentile99.ToTestFuznDuration()}", 1),
                        new AdvancedTableCell(string.Empty, 3)
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
                    new AdvancedTableCell("Total", 1),
                    new KeyValueCell("Count:", $"{stepResult.Count}", 1),
                    new AdvancedTableCell(string.Empty, 3)
                } });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Ok", 1),
                        new KeyValueCell("Count:", $"{stepResult.Ok.Count}", 1),
                        new KeyValueCell("Per Second:", $"{stepResult.Ok.PerSecond}", 1),
                        new AdvancedTableCell(string.Empty, 2)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Failed", 1),
                        new KeyValueCell("Count:", $"{stepResult.Failed.Count}", 1),
                        new KeyValueCell("Per Second:", $"{(stepResult.Failed.Count > 0 ? stepResult.Failed.PerSecond : "N/A")}", 1),
                        new AdvancedTableCell(string.Empty, 2)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell("Response Time - Ok", 1),
                        new KeyValueCell("Min:", $"{stepResult.Ok.DurationMin.ToTestFuznDuration()}", 1),
                        new KeyValueCell("Mean:", $"{stepResult.Ok.DurationMean.ToTestFuznDuration()}", 1),
                        new KeyValueCell("Max:", $"{stepResult.Ok.DurationMax.ToTestFuznDuration()}", 1),
                        new KeyValueCell("StdDev:", $"{stepResult.Ok.DurationStandardDeviation.ToTestFuznDuration()}", 1)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(string.Empty, 1),
                        new KeyValueCell("Median:", $"{stepResult.Ok.DurationMedian.ToTestFuznDuration()}", 1),
                        new KeyValueCell("P75:", $"{stepResult.Ok.DurationPercentile75.ToTestFuznDuration()}", 1),
                        new KeyValueCell("P90:", $"{stepResult.Ok.DurationPercentile90.ToTestFuznDuration()}", 1),
                        new KeyValueCell("P95:", $"{stepResult.Ok.DurationPercentile95.ToTestFuznDuration()}", 1)
                    }
                });
                table.Rows.Add(new AdvancedTableRow
                {
                    Cells =
                    {
                        new AdvancedTableCell(string.Empty, 1),
                        new KeyValueCell("P99:", $"{stepResult.Ok.DurationPercentile99.ToTestFuznDuration()}", 1),
                        new AdvancedTableCell(string.Empty, 3)
                    }
                });
                if (stepResult.Failed.Count > 0)
                {
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell("Response Time - Failed", 1),
                            new KeyValueCell("Min:", $"{stepResult.Failed.DurationMin.ToTestFuznDuration()}", 1),
                            new KeyValueCell("Mean:", $"{stepResult.Failed.DurationMean.ToTestFuznDuration()}", 1),
                            new KeyValueCell("Max:", $"{stepResult.Failed.DurationMax.ToTestFuznDuration()}", 1),
                            new KeyValueCell("StdDev:", $"{stepResult.Failed.DurationStandardDeviation.ToTestFuznDuration()}", 1)
                        }
                    });
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell(string.Empty, 1),
                            new KeyValueCell("Median:", $"{stepResult.Failed.DurationMedian.ToTestFuznDuration()}", 1),
                            new KeyValueCell("P75:", $"{stepResult.Failed.DurationPercentile75.ToTestFuznDuration()}", 1),
                            new KeyValueCell("P90:", $"{stepResult.Failed.DurationPercentile90.ToTestFuznDuration()}", 1),
                            new KeyValueCell("P95:", $"{stepResult.Failed.DurationPercentile95.ToTestFuznDuration()}", 1)
                        }
                    });
                    table.Rows.Add(new AdvancedTableRow
                    {
                        Cells =
                        {
                            new AdvancedTableCell(string.Empty, 1),
                            new KeyValueCell("P99:", $"{stepResult.Failed.DurationPercentile99.ToTestFuznDuration()}", 1),
                            new AdvancedTableCell(string.Empty, 3)
                        }
                    });
                }
            }

            testFramework.WriteAdvancedTable(table);

            WriteReportLink(testFramework);

            //Assertion
            var assertionSection = new StringBuilder();

            if (loadResult.AssertWhileWarmingUpException != null)
            {
                assertionSection.AppendLine("[red]Assert exceptions:[/]");

                assertionSection.AppendLine($"  [red]{loadResult.AssertWhileWarmingUpException.Message}[/]");
            }

            if (loadResult.AssertWhileRunningException != null)
            {
                if (assertionSection.Length == 0)
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
                testFramework.Write(Environment.NewLine);
                testFramework.WriteMarkup(assertionSection.ToString());
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
                        errorSection.AppendLine($"  [red]{Fuzn.TestFuzn.Internals.Terminal.MarkupParser.Escape(string.IsNullOrEmpty(error.Value.Message) ? error.Key : error.Value.Message)} (Count: {error.Value.Count})[/]");
                }
            }
            if (errorSection.Length > 0)
            {
                testFramework.Write(Environment.NewLine);
                testFramework.WriteMarkup(errorSection.ToString());
            }
        }
    }
}
