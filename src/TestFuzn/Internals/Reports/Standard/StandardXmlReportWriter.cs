using System.Text;
using System.Xml;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn.Internals.Reports.Standard;

internal class StandardXmlReportWriter : IStandardReport
{
    public async Task WriteReport(StandardReportData reportData)
    {
        try
        {
            var filePath = Path.Combine(reportData.TestsOutputDirectory, "TestReport.xml");

            var stringBuilder = new StringBuilder();
            using (var writer = XmlWriter.Create(stringBuilder, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("TestRun");
                writer.WriteElementString("Type", "Standard");
                writer.WriteElementString("Version", "1.0");
                writer.WriteElementString("ToolName", "TestFuzn");
                writer.WriteElementString("ToolVersion", typeof(StandardXmlReportWriter).Assembly.GetName().Version?.ToString() ?? "Unknown");
                writer.WriteElementString("GeneratedOn", DateTime.UtcNow.ToString("o"));
                writer.WriteElementString("TestRunId", reportData.TestRunId);
                writer.WriteElementString("ExecutionEnvironment", GlobalState.ExecutionEnvironment);
                writer.WriteElementString("TargetEnvironment", GlobalState.TargetEnvironment);
                writer.WriteElementString("StartTime", reportData.TestRunStartTime.ToString("o"));
                writer.WriteElementString("EndTime", reportData.TestRunEndTime.ToString("o"));

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

            await File.WriteAllTextAsync(filePath, stringBuilder.ToString());
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

        writer.WriteElementString("StartTime", testResult.StartTime.ToString("o"));
        writer.WriteElementString("EndTime", testResult.EndTime.ToString("o"));
        writer.WriteElementString("Status", testResult.Status.ToString());
        writer.WriteElementString("Duration", testResult.Duration.ToString(@"hh\:mm\:ss\.fff"));

        if (testResult.ScenarioResult != null)
            WriteScenario(writer, testResult.ScenarioResult);

        writer.WriteEndElement();
    }

    private void WriteScenario(XmlWriter writer, ScenarioStandardResult scenarioResult)
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
        writer.WriteElementString("Status", iterationResult.Passed ? "Passed" : "Failed");

        if (iterationResult.InputData is not null)
            writer.WriteElementString("InputData", iterationResult.InputData);

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
}
