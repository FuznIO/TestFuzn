using System.Text;
using System.Xml;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Feature;

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
                writer.WriteStartElement("TestRunResults");

                writer.WriteElementString("Version", "1.0");

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
                }
                writer.WriteEndElement();

                writer.WriteElementString("TestRunId", reportData.TestRunId);
                writer.WriteElementString("TargetEnvironment", GlobalState.TargetEnvironment);
                writer.WriteElementString("ExecutionEnvironment", GlobalState.ExecutionEnvironment);

                foreach (var featureResult in reportData.GroupResults.Values)
                {
                    WriteGroup(writer, featureResult);
                }

                writer.WriteEndElement(); // TestRunResults
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

        foreach (var testResult in groupResult.TestResults)
        {
            WriteTest(writer, testResult.Value);

            
        }

        writer.WriteEndElement();
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

        if (scenarioResult.HasInputData)
        {
            foreach (var iterationResult in scenarioResult.IterationResults)
            {
                WriteIteration(writer, iterationResult);
            }
        }
        else
        {
            var iterationResult = scenarioResult.IterationResults.FirstOrDefault();
            if (iterationResult != null)
                WriteSteps(writer, iterationResult.StepResults.Values.ToList());
        }

        writer.WriteEndElement();
    }

    private void WriteIteration(XmlWriter writer, IterationResult iterationResult)
    {
        writer.WriteStartElement("Iteration");

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
