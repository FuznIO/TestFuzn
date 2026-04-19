using System.Text;
using System.Xml;
using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Reports.Standard;

internal class StandardXmlReportWriter : IStandardReport
{
    private readonly IFileSystem _fileSystem;
    private readonly TestSession _testSession;

    public StandardXmlReportWriter(IFileSystem fileSystem,
        TestSession testSession)
    {
        _fileSystem = fileSystem;
        _testSession = testSession;
    }

    public async Task WriteReport(StandardReportData reportData)
    {
        try
        {
            var directory = Path.Combine(reportData.TestsResultsDirectory, "Data");
            _fileSystem.CreateDirectory(directory);
            var filePath = Path.Combine(directory, "TestReport.xml");

            using var memoryStream = new MemoryStream();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };
            using (var writer = XmlWriter.Create(memoryStream, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("TestRun");
                writer.WriteElementString("Type", "Standard");
                writer.WriteElementString("Version", "1.0");
                writer.WriteElementString("ToolName", "TestFuzn");
                writer.WriteElementString("ToolVersion", typeof(StandardXmlReportWriter).Assembly.GetName().Version?.ToString() ?? "Unknown");
                writer.WriteElementString("GeneratedOn", DateTime.UtcNow.ToString("o"));
                writer.WriteElementString("TestRunId", reportData.TestRunId);
                writer.WriteElementString("ExecutionEnvironment", _testSession.Configuration?.ExecutionEnvironment);
                writer.WriteElementString("TargetEnvironment", _testSession.Configuration?.TargetEnvironment);
                writer.WriteElementString("StartTime", reportData.TestRunStartTime.ToString("o"));
                writer.WriteElementString("EndTime", reportData.TestRunEndTime.ToString("o"));

                var runSummary = ComputeSummary(reportData.GroupResults.Values);
                WriteSummary(writer, runSummary);

                writer.WriteStartElement("Suite");
                {
                    writer.WriteElementString("Name", reportData.Suite.Name);
                    writer.WriteElementString("Id", reportData.Suite.Id);

                    if (reportData.Suite.Metadata != null)
                    {
                        writer.WriteStartElement("Metadata");
                        foreach (var metadata in reportData.Suite.Metadata)
                        {
                            writer.WriteStartElement("Property");
                            writer.WriteElementString("Key", metadata.Key);
                            writer.WriteElementString("Value", metadata.Value);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }

                    WriteSummary(writer, runSummary);

                    writer.WriteStartElement("Groups");
                    foreach (var standardResult in reportData.GroupResults.Values)
                    {
                        WriteGroup(writer, standardResult);
                    }
                    writer.WriteEndElement(); // Groups
                }
                writer.WriteEndElement(); // Suite

                writer.WriteEndElement(); // TestRun
                writer.WriteEndDocument();
            }

            await _fileSystem.WriteAllBytesAsync(filePath, memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write XML report.", ex);
        }
    }

    private void WriteGroup(XmlWriter writer, GroupResult groupResult)
    {
        writer.WriteStartElement("Group");

        writer.WriteElementString("Name", groupResult.Name);

        WriteSummary(writer, ComputeSummary(groupResult));

        writer.WriteStartElement("Tests");
        foreach (var testResult in groupResult.TestResults)
        {
            WriteTest(writer, testResult.Value);
        }
        writer.WriteEndElement(); // Tests

        writer.WriteEndElement(); // Group
    }

    private void WriteTest(XmlWriter writer, TestResult testResult)
    {
        writer.WriteStartElement("Test");
        writer.WriteElementString("Name", testResult.Name);
        writer.WriteElementString("Id", testResult.Id);

        if (!string.IsNullOrEmpty(testResult.Description))
            writer.WriteElementString("Description", testResult.Description);

        if (testResult.Tags != null && testResult.Tags.Count > 0)
        {
            writer.WriteStartElement("Tags");
            foreach (var tag in testResult.Tags)
            {
                writer.WriteElementString("Tag", tag);
            }
            writer.WriteEndElement();
        }

        if (testResult.Metadata != null)
        {
            writer.WriteStartElement("Metadata");
            foreach (var metadata in testResult.Metadata)
            {
                writer.WriteStartElement("Property");
                writer.WriteElementString("Key", metadata.Key);
                writer.WriteElementString("Value", metadata.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        writer.WriteElementString("Status", testResult.Status.ToString());
        writer.WriteElementString("TestType", testResult.TestType.ToString());

        writer.WriteElementString("StartTime", testResult.StartTime().ToString("o"));
        writer.WriteElementString("EndTime", testResult.EndTime().ToString("o"));
        writer.WriteElementString("Duration", testResult.TestRunDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("InitStartTime", testResult.InitStartTime.ToString("o"));
        writer.WriteElementString("InitEndTime", testResult.InitEndTime.ToString("o"));
        writer.WriteElementString("InitDuration", testResult.InitDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("ExecuteStartTime", testResult.ExecuteStartTime.ToString("o"));
        writer.WriteElementString("ExecuteEndTime", testResult.ExecuteEndTime.ToString("o"));
        writer.WriteElementString("ExecuteDuration", testResult.ExecuteDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("CleanupStartTime", testResult.CleanupStartTime.ToString("o"));
        writer.WriteElementString("CleanupEndTime", testResult.CleanupEndTime.ToString("o"));
        writer.WriteElementString("CleanupDuration", testResult.CleanupDuration().ToString(@"hh\:mm\:ss\.fff"));

        if (testResult.TestType == TestType.Load)
        {
            var reportName = FileNameHelper.MakeFilenameSafe($"{testResult.Group.Name}-{testResult.Name}");
            writer.WriteElementString("LoadReportPath", $"{reportName}.xml");
        }
        else
        {
            WriteScenario(writer, testResult);
        }

        writer.WriteEndElement();
    }

    private void WriteScenario(XmlWriter writer, TestResult scenarioResult)
    {
        writer.WriteStartElement("Scenario");

        var status = scenarioResult.Status.ToString();

        writer.WriteElementString("Status", status);

        writer.WriteStartElement("Iterations");
        for (int i = 0; i < scenarioResult.IterationResults.Count; i++)
        {
            WriteIteration(writer, scenarioResult.IterationResults[i], i);
        }
        writer.WriteEndElement(); // Iterations

        writer.WriteEndElement();
    }

    private void WriteIteration(XmlWriter writer, IterationResult iterationResult, int index)
    {
        writer.WriteStartElement("Iteration");

        writer.WriteElementString("Index", index.ToString());
        writer.WriteElementString("CorrelationId", iterationResult.CorrelationId);
        writer.WriteElementString("Status", iterationResult.Passed ? "Passed" : "Failed");

        if (iterationResult.InputData is not null)
        {
            writer.WriteElementString("InputData", iterationResult.InputData.ToString() ?? string.Empty);

            if (iterationResult.InputData is IReportableInputData reportable)
            {
                var properties = reportable.ToReportProperties();
                if (properties != null && properties.Count > 0)
                {
                    writer.WriteStartElement("InputDataProperties");
                    foreach (var property in properties)
                    {
                        writer.WriteStartElement("Property");
                        writer.WriteElementString("Key", property.Key);
                        writer.WriteElementString("Value", property.Value);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }
        }

        writer.WriteElementString("InitStartTime", iterationResult.InitStartTime.ToString("o"));
        writer.WriteElementString("InitEndTime", iterationResult.InitEndTime.ToString("o"));
        writer.WriteElementString("InitDuration", iterationResult.InitDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("ExecuteStartTime", iterationResult.ExecuteStartTime.ToString("o"));
        writer.WriteElementString("ExecuteEndTime", iterationResult.ExecuteEndTime.ToString("o"));
        writer.WriteElementString("ExecuteDuration", iterationResult.ExecuteDuration().ToString(@"hh\:mm\:ss\.fff"));

        writer.WriteElementString("CleanupStartTime", iterationResult.CleanupStartTime.ToString("o"));
        writer.WriteElementString("CleanupEndTime", iterationResult.CleanupEndTime.ToString("o"));
        writer.WriteElementString("CleanupDuration", iterationResult.CleanupDuration().ToString(@"hh\:mm\:ss\.fff"));

        WriteSteps(writer, iterationResult.StepResults.Values.ToList());

        writer.WriteEndElement();
    }

    private void WriteSteps(XmlWriter writer, List<StepStandardResult> steps)
    {
        writer.WriteStartElement("Steps");
        foreach (var step in steps)
        {
            WriteStep(writer, step);
        }
        writer.WriteEndElement();
    }

    private void WriteStep(XmlWriter writer, StepStandardResult step)
    {
        writer.WriteStartElement("Step");
        writer.WriteElementString("Name", step.Name);
        writer.WriteElementString("Id", step.Id);
        writer.WriteElementString("Status", step.Status.ToString());
        writer.WriteElementString("Duration", step.Duration.ToString(@"hh\:mm\:ss\.fff"));

        if (step.Status == StepStatus.Failed && step.Exception != null)
            WriteFailure(writer, step.Exception);

        if (step.Comments != null && step.Comments.Count > 0)
        {
            writer.WriteStartElement("Comments");
            foreach (var comment in step.Comments)
            {
                writer.WriteStartElement("Comment");
                writer.WriteElementString("Text", comment.Text);
                writer.WriteElementString("Timestamp", comment.Created.ToString("o"));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        if (step.Attachments != null && step.Attachments.Count > 0)
        {
            writer.WriteStartElement("Attachments");
            foreach (var attachment in step.Attachments)
            {
                writer.WriteStartElement("Attachment");
                writer.WriteElementString("Name", attachment.Name);
                writer.WriteElementString("Path", attachment.Path);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        if (step.StepResults != null && step.StepResults.Count > 0)
            WriteSteps(writer, step.StepResults);

        writer.WriteEndElement();
    }

    private void WriteFailure(XmlWriter writer, Exception exception)
    {
        writer.WriteStartElement("Failure");
        WriteExceptionBody(writer, exception);
        writer.WriteEndElement();
    }

    private void WriteExceptionBody(XmlWriter writer, Exception exception)
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

    private static Summary ComputeSummary(IEnumerable<GroupResult> groupResults)
    {
        var summary = new Summary();
        foreach (var group in groupResults)
            Accumulate(summary, group);
        return summary;
    }

    private static Summary ComputeSummary(GroupResult groupResult)
    {
        var summary = new Summary();
        Accumulate(summary, groupResult);
        return summary;
    }

    private static void Accumulate(Summary summary, GroupResult groupResult)
    {
        foreach (var test in groupResult.TestResults.Values)
        {
            summary.Total++;
            switch (test.Status)
            {
                case TestStatus.Passed:
                    summary.Passed++;
                    break;
                case TestStatus.Failed:
                    summary.Failed++;
                    break;
                case TestStatus.Skipped:
                    summary.Skipped++;
                    break;
            }
        }
    }

    private static void WriteSummary(XmlWriter writer, Summary summary)
    {
        writer.WriteStartElement("Summary");
        writer.WriteElementString("Total", summary.Total.ToString());
        writer.WriteElementString("Passed", summary.Passed.ToString());
        writer.WriteElementString("Failed", summary.Failed.ToString());
        writer.WriteElementString("Skipped", summary.Skipped.ToString());
        writer.WriteEndElement();
    }

    private sealed class Summary
    {
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
    }
}
