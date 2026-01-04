using System.Text;
using System.Xml;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class LoadXmlReportWriter : ILoadReport
{
    public LoadXmlReportWriter()
    {
    }

    public async Task WriteReport(LoadReportData loadReportData)
    {
        try
        {
            var reportName = FileNameHelper.MakeFilenameSafe($"{loadReportData.Group.Name}-{loadReportData.Test.Name}");
            var filePath = Path.Combine(GlobalState.TestsOutputDirectory, $"LoadTestReport-{reportName}.xml");

            var stringBuilder = new StringBuilder();
            using (var writer = XmlWriter.Create(stringBuilder, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("LoadTestResults");
                writer.WriteElementString("Version", "1.0");

                writer.WriteStartElement("Suite");
                {
                    writer.WriteElementString("Name", loadReportData.TestSuite.Name);
                    writer.WriteElementString("Id", loadReportData.TestSuite.Id);

                    if (loadReportData.TestSuite.Metadata != null)
                    {
                        writer.WriteStartElement("Metadata");
                        foreach (var metadata in loadReportData.TestSuite.Metadata)
                        {
                            writer.WriteStartElement("Property");
                            writer.WriteElementString("Key", metadata.Key);
                            writer.WriteElementString("Value", metadata.Value);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                }
                writer.WriteEndElement();

                writer.WriteStartElement("Group");
                writer.WriteElementString("Name", loadReportData.Group.Name);
                writer.WriteEndElement();

                WriteTest(writer, loadReportData.Test);

                writer.WriteElementString("TestRunId", loadReportData.TestRunId);
                writer.WriteElementString("TargetEnvironment", GlobalState.TargetEnvironment);
                writer.WriteElementString("ExecutionEnvironment", GlobalState.ExecutionEnvironment);

                writer.WriteStartElement("Scenarios");
                foreach (var scenarioResult in loadReportData.ScenarioResults)
                {
                    WriteScenario(writer, loadReportData.Group.Name, scenarioResult);
                }
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            foreach (var scenarioResult in loadReportData.ScenarioResults)
            {
                InMemorySnapshotCollectorSinkPlugin.RemoveSnapshots(loadReportData.Group.Name, scenarioResult.ScenarioName);
            }

            await File.WriteAllTextAsync(filePath, stringBuilder.ToString());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write XML load test report.", ex);
        }
    }

    private void WriteTest(XmlWriter writer, Contracts.Reports.TestInfo test)
    {
        writer.WriteStartElement("Test");
        writer.WriteElementString("Name", test.Name);
        writer.WriteElementString("FullName", test.FullName);
        writer.WriteElementString("Id", test.Id);

        if (test.Tags != null && test.Tags.Count > 0)
        {
            writer.WriteStartElement("Tags");
            foreach (var tag in test.Tags)
            {
                writer.WriteElementString("Tag", tag);
            }
            writer.WriteEndElement();
        }

        if (test.Metadata != null && test.Metadata.Count > 0)
        {
            writer.WriteStartElement("Metadata");
            foreach (var metadata in test.Metadata)
            {
                writer.WriteStartElement("Property");
                writer.WriteElementString("Key", metadata.Key);
                writer.WriteElementString("Value", metadata.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private void WriteScenario(XmlWriter writer, string featureName, ScenarioLoadResult scenarioResult)
    {
        writer.WriteStartElement("Scenario");
        writer.WriteElementString("Name", scenarioResult.ScenarioName);
        writer.WriteElementString("Id", scenarioResult.Id);
        writer.WriteElementString("TotalExecutionDuration", scenarioResult.TotalExecutionDuration.ToString(@"hh\:mm\:ss\.fff"));

        if (scenarioResult.Tags != null && scenarioResult.Tags.Count > 0)
        {
            writer.WriteStartElement("Tags");
            foreach (var tag in scenarioResult.Tags)
            {
                writer.WriteElementString("Tag", tag);
            }
            writer.WriteEndElement();
        }

        if (scenarioResult.Metadata != null)
        {
            writer.WriteStartElement("Metadata");
            foreach (var metadata in scenarioResult.Metadata)
            {
                writer.WriteStartElement("Property");
                writer.WriteElementString("Key", metadata.Key);
                writer.WriteElementString("Value", metadata.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        writer.WriteStartElement("Simulations");
        foreach (var simulation in scenarioResult.Simulations)
        {
            writer.WriteElementString("Simulation", simulation);
        }
        writer.WriteEndElement();

        WriteStats(writer, scenarioResult.Ok, scenarioResult.Failed);

        WriteSteps(writer, scenarioResult.Steps.Values.ToList());

        WriteSnapshots(writer, featureName, scenarioResult.ScenarioName);

        writer.WriteEndElement();
    }

    private void WriteSteps(XmlWriter writer, List<StepLoadResult> steps)
    {
        writer.WriteStartElement("Steps");
        foreach (var step in steps)
            WriteStep(writer, step);
        writer.WriteEndElement();
    }

    private void WriteStep(XmlWriter writer, StepLoadResult stepResult)
    {
        writer.WriteStartElement("Step");
        writer.WriteElementString("Name", stepResult.Name);
        writer.WriteElementString("Id", stepResult.Id);
        WriteStats(writer, stepResult.Ok, stepResult.Failed);
        writer.WriteEndElement();

        if (stepResult.Errors != null && stepResult.Errors.Count > 0)
        {
            writer.WriteStartElement("Errors");
            foreach (var error in stepResult.Errors.Values)
            {
                writer.WriteStartElement("Error");
                writer.WriteElementString("Message", error.Message);
                writer.WriteElementString("Count", error.Count.ToString());
                writer.WriteElementString("Details", error.Details);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        if (stepResult.Steps != null && stepResult.Steps.Count > 0)
            WriteSteps(writer, stepResult.Steps);
    }

    public void WriteStats(XmlWriter writer, Stats statsOk, Stats statsFailed)
    {
        writer.WriteStartElement("Ok");
        Write(statsOk);
        writer.WriteEndElement();
        writer.WriteStartElement("Failed");
        Write(statsFailed);
        writer.WriteEndElement();

        void Write(Stats stats)
        {
            writer.WriteElementString("TotalExecutionDuration", stats.TotalExecutionDuration.ToString(@"hh\:mm\:ss\.fff"));
            writer.WriteElementString("RequestCount", stats.RequestCount.ToString());
            writer.WriteElementString("RequestsPerSecond", stats.RequestsPerSecond.ToString());
            writer.WriteElementString("Mean", stats.ResponseTimeMean.ToString());
            writer.WriteElementString("Min", stats.ResponseTimeMin.ToString());
            writer.WriteElementString("Max", stats.ResponseTimeMax.ToString());
            writer.WriteElementString("StdDev", stats.ResponseTimeStandardDeviation.ToString());
            writer.WriteElementString("Median", stats.ResponseTimeMedian.ToString());
            writer.WriteElementString("P75", stats.ResponseTimePercentile75.ToString());
            writer.WriteElementString("P95", stats.ResponseTimePercentile95.ToString());
            writer.WriteElementString("P99", stats.ResponseTimePercentile99.ToString());
        }
    }

    private void WriteSnapshots(XmlWriter writer, string featureName, string scenarioName)
    {
        var snapshots = InMemorySnapshotCollectorSinkPlugin.GetSnapshots(featureName, scenarioName);

        writer.WriteStartElement("Snapshots");

        foreach (var scenarioResult in snapshots)
        {
            writer.WriteStartElement("Snapshot");
            writer.WriteElementString("Created", scenarioResult.Created.ToString("o"));
            writer.WriteElementString("TotalExecutionDuration", scenarioResult.TotalExecutionDuration.ToString(@"hh\:mm\:ss\.fff"));
            WriteStats(writer, scenarioResult.Ok, scenarioResult.Failed);
            WriteSteps(writer, scenarioResult.Steps.Values.ToList());
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }
}
