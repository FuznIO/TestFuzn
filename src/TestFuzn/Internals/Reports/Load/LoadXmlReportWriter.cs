using System.Text;
using System.Globalization;
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

                if (loadReportData.Workers.Count > 0)
                {
                    var inv = CultureInfo.InvariantCulture;
                    writer.WriteStartElement("Workers");
                    foreach (var worker in loadReportData.Workers)
                    {
                        writer.WriteStartElement("Worker");
                        writer.WriteElementString("Id", worker.Id);
                        writer.WriteElementString("State", worker.State);
                        writer.WriteElementString("Os", worker.Os);
                        writer.WriteElementString("CpuCores", worker.CpuCores.ToString(inv));
                        writer.WriteElementString("Iterations", worker.Iterations.ToString(inv));
                        writer.WriteElementString("Errors", worker.Errors.ToString(inv));
                        writer.WriteElementString("CpuAveragePercent", worker.CpuAveragePercent.ToString(inv));
                        writer.WriteElementString("CpuMaxPercent", worker.CpuMaxPercent.ToString(inv));
                        writer.WriteElementString("MemoryMaxMb", worker.MemoryMaxMb.ToString(inv));
                        writer.WriteElementString("IterationStartLagMaxMs", worker.IterationStartLagMaxMs.ToString(inv));
                        writer.WriteElementString("CpuWarning", worker.HasCpuWarning ? "true" : "false");
                        writer.WriteElementString("LogFolder", worker.LogFolder);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }

                if (loadReportData.Warnings.Count > 0)
                {
                    writer.WriteStartElement("Warnings");
                    foreach (var warning in loadReportData.Warnings)
                        writer.WriteElementString("Warning", warning);
                    writer.WriteEndElement();
                }

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

        if (scenarioResult.Hooks.Count > 0)
        {
            writer.WriteStartElement("Hooks");
            foreach (var hook in scenarioResult.Hooks)
                WriteStep(writer, hook);
            writer.WriteEndElement();
        }

        WriteSnapshots(writer, data, scenarioResult.ScenarioName);
        WriteTimeline(writer, data, scenarioResult.ScenarioName);
        WriteDistribution(writer, data, scenarioResult.ScenarioName);

        writer.WriteEndElement();
    }

    private static void WriteTimeline(XmlWriter writer, LoadReportData data, string scenarioName)
    {
        if (!data.Timelines.TryGetValue(scenarioName, out var timeline))
            return;

        writer.WriteStartElement("Timeline");
        foreach (var point in timeline)
        {
            writer.WriteStartElement("Point");
            writer.WriteElementString("Time", point.Time.ToString("o"));
            writer.WriteElementString("ElapsedMs", XmlConvert.ToString(point.ElapsedMs));
            writer.WriteElementString("Seconds", XmlConvert.ToString(point.Seconds));
            writer.WriteElementString("OkCount", XmlConvert.ToString(point.OkCount));
            writer.WriteElementString("FailedCount", XmlConvert.ToString(point.FailedCount));
            writer.WriteElementString("OkPerSecond", XmlConvert.ToString(point.OkPerSecond));
            writer.WriteElementString("FailedPerSecond", XmlConvert.ToString(point.FailedPerSecond));
            WriteOptional(writer, "MeanMs", point.MeanMs);
            WriteOptional(writer, "P50Ms", point.P50Ms);
            WriteOptional(writer, "P90Ms", point.P90Ms);
            WriteOptional(writer, "P95Ms", point.P95Ms);
            WriteOptional(writer, "P99Ms", point.P99Ms);
            WriteOptional(writer, "VirtualUsers", point.VirtualUsers);
            WriteOptional(writer, "TargetPerSecond", point.TargetPerSecond);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteDistribution(XmlWriter writer, LoadReportData data, string scenarioName)
    {
        if (!data.Distributions.TryGetValue(scenarioName, out var buckets) || buckets.Count == 0)
            return;

        writer.WriteStartElement("Distribution");
        foreach (var bucket in buckets)
        {
            writer.WriteStartElement("Bucket");
            writer.WriteElementString("FromMs", XmlConvert.ToString(bucket.FromMs));
            writer.WriteElementString("ToMs", XmlConvert.ToString(bucket.ToMs));
            writer.WriteElementString("OkCount", XmlConvert.ToString(bucket.OkCount));
            writer.WriteElementString("FailedCount", XmlConvert.ToString(bucket.FailedCount));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteOptional(XmlWriter writer, string name, double? value)
    {
        if (value.HasValue)
            writer.WriteElementString(name, XmlConvert.ToString(value.Value));
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

        if (stepResult.StatusCodes != null && stepResult.StatusCodes.Count > 0)
        {
            writer.WriteStartElement("StatusCodes");
            foreach (var code in stepResult.StatusCodes.OrderBy(code => code.Key))
            {
                writer.WriteStartElement("StatusCode");
                writer.WriteElementString("Code", code.Key.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteElementString("Count", code.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
            writer.WriteElementString("Count", stats.Count.ToString());
            writer.WriteElementString("PerSecond", stats.PerSecond.ToString());
            writer.WriteElementString("Mean", stats.DurationMean.ToString());
            writer.WriteElementString("Min", stats.DurationMin.ToString());
            writer.WriteElementString("Max", stats.DurationMax.ToString());
            writer.WriteElementString("StdDev", stats.DurationStandardDeviation.ToString());
            writer.WriteElementString("Median", stats.DurationMedian.ToString());
            writer.WriteElementString("P75", stats.DurationPercentile75.ToString());
            writer.WriteElementString("P90", stats.DurationPercentile90.ToString());
            writer.WriteElementString("P95", stats.DurationPercentile95.ToString());
            writer.WriteElementString("P99", stats.DurationPercentile99.ToString());
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
