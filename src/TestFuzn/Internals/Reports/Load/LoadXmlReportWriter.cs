using System.Text;
using System.Xml;
using FuznLabs.TestFuzn.Internals.State;
using FuznLabs.TestFuzn.Contracts.Reports;
using FuznLabs.TestFuzn.Contracts.Results.Load;

namespace FuznLabs.TestFuzn.Internals.Reports.Load;

internal class LoadXmlReportWriter : ILoadReport
{
    public LoadXmlReportWriter()
    { 
    }

    public async Task WriteReport(LoadReportData loadReportData)
    {
        try
        {
            var reportName = FileNameHelper.MakeFilenameSafe(loadReportData.ScenarioResult.ScenarioName);
            var filePath = Path.Combine(GlobalState.TestsOutputDirectory, $"TestFusion_Report_Load_{reportName}.xml");

            var stringBuilder = new StringBuilder();
            using (var writer = XmlWriter.Create(stringBuilder, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("LoadTestResults");
                writer.WriteAttributeString("Version", "1.0");

                WriteScenario(writer, loadReportData.FeatureName, loadReportData.ScenarioResult);

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            InMemorySnapshotCollectorSinkPlugin.RemoveSnapshots(loadReportData.FeatureName, loadReportData.ScenarioResult.ScenarioName);

            await File.WriteAllTextAsync(filePath, stringBuilder.ToString());
        }
        catch (Exception ex)
        {
            // Log or handle the exception as needed
            throw new InvalidOperationException("Failed to write XML load test report.", ex);
        }
    }

    private void WriteScenario(XmlWriter writer, string featureName, ScenarioLoadResult scenarioResult)
    {
        writer.WriteStartElement("Scenario");
        writer.WriteAttributeString("Name", scenarioResult.ScenarioName);
        writer.WriteElementString("TotalExecutionDuration", scenarioResult.TotalExecutionDuration.ToString(@"hh\:mm\:ss\.fff"));
        WriteStats(writer, scenarioResult.Ok, scenarioResult.Failed);

        WriteSteps(writer, scenarioResult);

        WriteSnapshots(writer, featureName, scenarioResult.ScenarioName);

        writer.WriteEndElement();
    }

    private void WriteSteps(XmlWriter writer, ScenarioLoadResult scenarioResult)
    {
        writer.WriteStartElement("Steps");
        foreach (var step in scenarioResult.Steps)
        {
            var stepResult = step.Value;
            writer.WriteStartElement("Step");
            writer.WriteAttributeString("Name", stepResult.Name);
            WriteStats(writer, stepResult.Ok, stepResult.Failed);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
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
            writer.WriteAttributeString("created", scenarioResult.Created.ToString("o"));

            writer.WriteElementString("TotalExecutionDuration", scenarioResult.TotalExecutionDuration.ToString(@"hh\:mm\:ss\.fff"));

            WriteStats(writer, scenarioResult.Ok, scenarioResult.Failed);

            WriteSteps(writer, scenarioResult);

            writer.WriteEndElement();
        }

        writer.WriteEndElement();

    }
}
