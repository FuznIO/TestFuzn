using System.Text;
using System.Xml;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn.Internals.Reports.Load;

internal class LoadXmlReportWriter : ILoadReport
{
    private readonly IFileSystem _fileSystem;
    private readonly TestSession _testSession;

    public LoadXmlReportWriter(IFileSystem fileSystem,
        TestSession testSession)
    {
        _fileSystem = fileSystem;
        _testSession = testSession;
    }

    public async Task WriteReport(LoadReportData loadReportData)
    {
        try
        {
            var reportName = FileNameHelper.MakeFilenameSafe($"{loadReportData.Test.Group.Name}-{loadReportData.Test.Name}");
            var directory = Path.Combine(_testSession.TestsResultsDirectory, "Data");
            _fileSystem.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"{reportName}.xml");

            using var memoryStream = new MemoryStream();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };
            using (var writer = XmlWriter.Create(memoryStream, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("LoadTestRunResults");
                writer.WriteElementString("Version", "1.0");

                writer.WriteStartElement("Suite");
                {
                    writer.WriteElementString("Name", loadReportData.Suite.Name);
                    writer.WriteElementString("Id", loadReportData.Suite.Id);

                    if (loadReportData.Suite.Metadata != null)
                    {
                        writer.WriteStartElement("Metadata");
                        foreach (var metadata in loadReportData.Suite.Metadata)
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
                writer.WriteElementString("Name", loadReportData.Test.Group.Name);
                writer.WriteEndElement();

                writer.WriteElementString("TestRunId", loadReportData.TestRunId);
                writer.WriteElementString("TargetEnvironment", _testSession.Configuration?.TargetEnvironment);
                writer.WriteElementString("ExecutionEnvironment", _testSession.Configuration?.ExecutionEnvironment);

                WriteTest(writer, loadReportData.Test);

                writer.WriteStartElement("Scenarios");
                foreach (var scenarioResult in loadReportData.ScenarioResults)
                {
                    WriteScenario(writer, loadReportData, scenarioResult);
                }
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            await _fileSystem.WriteAllBytesAsync(filePath, memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write XML load test report.", ex);
        }
    }

    private void WriteTest(XmlWriter writer, TestResult test)
    {
        writer.WriteStartElement("Test");
        writer.WriteElementString("Name", test.Name);
        writer.WriteElementString("FullName", test.FullName);
        writer.WriteElementString("Id", test.Id);

        if (!string.IsNullOrEmpty(test.Description))
            writer.WriteElementString("Description", test.Description);

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

        writer.WriteElementString("Status", test.Status.ToString());

        writer.WriteElementString("StartTime", test.StartTime().ToString("o"));
        writer.WriteElementString("EndTime", test.EndTime().ToString("o"));
        writer.WriteElementString("Duration", test.TestRunDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("InitStartTime", test.InitStartTime.ToString("o"));
        writer.WriteElementString("InitEndTime", test.InitEndTime.ToString("o"));
        writer.WriteElementString("InitDuration", test.InitDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("ExecuteStartTime", test.ExecuteStartTime.ToString("o"));
        writer.WriteElementString("ExecuteEndTime", test.ExecuteEndTime.ToString("o"));
        writer.WriteElementString("ExecuteDuration", test.ExecuteDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("CleanupStartTime", test.CleanupStartTime.ToString("o"));
        writer.WriteElementString("CleanupEndTime", test.CleanupEndTime.ToString("o"));
        writer.WriteElementString("CleanupDuration", test.CleanupDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteEndElement();
    }

    private void WriteScenario(XmlWriter writer, LoadReportData data, ScenarioLoadResult scenarioResult)
    {
        writer.WriteStartElement("Scenario");
        writer.WriteElementString("Name", scenarioResult.ScenarioName);
        writer.WriteElementString("Id", scenarioResult.Id);
        writer.WriteElementString("Description", scenarioResult.Description);
        writer.WriteElementString("Status", scenarioResult.Status.ToString());

        writer.WriteElementString("StartTime", scenarioResult.StartTime().ToString("o"));
        writer.WriteElementString("EndTime", scenarioResult.EndTime().ToString("o"));
        writer.WriteElementString("Duration", scenarioResult.TestRunTotalDuration().ToString(@"hh\:mm\:ss\.fff"));
        writer.WriteElementString("TotalExecutionDuration", scenarioResult.TotalExecutionDuration.ToString());

        writer.WriteElementString("InitStartTime", scenarioResult.InitStartTime.ToString("o"));
        writer.WriteElementString("InitEndTime", scenarioResult.InitEndTime.ToString("o"));
        writer.WriteElementString("InitDuration", scenarioResult.InitTotalDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("WarmupStartTime", scenarioResult.WarmupStartTime.ToString("o"));
        writer.WriteElementString("WarmupEndTime", scenarioResult.WarmupEndTime.ToString("o"));
        writer.WriteElementString("WarmupDuration", scenarioResult.WarmupTotalDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("MeasurementStartTime", scenarioResult.MeasurementStartTime.ToString("o"));
        writer.WriteElementString("MeasurementEndTime", scenarioResult.MeasurementEndTime.ToString("o"));
        writer.WriteElementString("MeasurementDuration", scenarioResult.MeasurementTotalDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("CleanupStartTime", scenarioResult.CleanupStartTime.ToString("o"));
        writer.WriteElementString("CleanupEndTime", scenarioResult.CleanupEndTime.ToString("o"));
        writer.WriteElementString("CleanupDuration", scenarioResult.CleanupTotalDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteStartElement("Simulations");
        foreach (var simulation in scenarioResult.Simulations)
        {
            writer.WriteElementString("Simulation", simulation);
        }
        writer.WriteEndElement();

        WriteStats(writer, scenarioResult.Ok, scenarioResult.Failed);

        if (scenarioResult.AssertWhileWarmingUpException != null)
        {
            writer.WriteStartElement("AssertWhileWarmingUpFailure");
            WriteExceptionBody(writer, scenarioResult.AssertWhileWarmingUpException);
            writer.WriteEndElement();
        }

        if (scenarioResult.AssertWhileRunningException != null)
        {
            writer.WriteStartElement("AssertWhileRunningFailure");
            WriteExceptionBody(writer, scenarioResult.AssertWhileRunningException);
            writer.WriteEndElement();
        }

        if (scenarioResult.AssertWhenDoneException != null)
        {
            writer.WriteStartElement("AssertWhenDoneFailure");
            WriteExceptionBody(writer, scenarioResult.AssertWhenDoneException);
            writer.WriteEndElement();
        }

        WriteSteps(writer, scenarioResult.Steps.Values.ToList());

        WriteSnapshots(writer, data, scenarioResult.ScenarioName);

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
            writer.WriteElementString("TotalExecutionDuration", stats.TotalExecutionDuration.ToString());
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

    private static void WriteExceptionBody(XmlWriter writer, Exception exception)
    {
        writer.WriteElementString("Message", exception.Message);
        writer.WriteElementString("Type", exception.GetType().FullName);
        if (exception.StackTrace != null)
            writer.WriteElementString("StackTrace", exception.StackTrace);
        if (exception.InnerException != null)
        {
            writer.WriteStartElement("InnerException");
            WriteExceptionBody(writer, exception.InnerException);
            writer.WriteEndElement();
        }
    }

    private void WriteSnapshots(XmlWriter writer, LoadReportData data, string scenarioName)
    {
        var snapshots = data.Snapshots[scenarioName];

        writer.WriteStartElement("Snapshots");

        foreach (var scenarioResult in snapshots)
        {
            writer.WriteStartElement("Snapshot");
            writer.WriteElementString("Created", scenarioResult.Created.ToString("o"));
            writer.WriteElementString("TotalExecutionDuration", scenarioResult.TotalExecutionDuration.ToString());
            WriteStats(writer, scenarioResult.Ok, scenarioResult.Failed);
            WriteSteps(writer, scenarioResult.Steps.Values.ToList());
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }
}
