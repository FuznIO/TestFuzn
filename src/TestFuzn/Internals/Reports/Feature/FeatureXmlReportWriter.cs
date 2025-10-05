using System.Text;
using System.Xml;
using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Contracts.Results.Feature;

namespace Fuzn.TestFuzn.Internals.Reports.Feature;

internal class FeatureXmlReportWriter : IFeatureReport
{
    public async Task WriteReport(FeatureReportData featureReportData)
    {
        try
        {
            var filePath = Path.Combine(featureReportData.TestsOutputDirectory, "TestFuzn_Report_Features.xml");

            var stringBuilder = new StringBuilder();
            using (var writer = XmlWriter.Create(stringBuilder, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("TestRunResults");

                writer.WriteElementString("Version", "1.0");

                writer.WriteStartElement("TestSuite");
                {
                    writer.WriteElementString("Name", featureReportData.TestSuite.Name);
                    writer.WriteElementString("Id", featureReportData.TestSuite.Id);

                    if (featureReportData.TestSuite.Metadata != null)
                    {
                        writer.WriteStartElement("Metadata");
                        foreach (var metadata in featureReportData.TestSuite.Metadata)
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

                writer.WriteElementString("TestRunId", featureReportData.TestRunId);

                foreach (var featureResult in featureReportData.Results.FeatureResults.Values)
                {
                    WriteFeature(writer, featureResult);
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

    private void WriteFeature(XmlWriter writer, FeatureResult featureResult)
    {
        writer.WriteStartElement("Feature");

        writer.WriteElementString("Name", featureResult.Name);
        if (!string.IsNullOrWhiteSpace(featureResult.Id))
            writer.WriteElementString("Id", featureResult.Id);

        if (featureResult.Metadata != null)
        {
            writer.WriteStartElement("Metadata");
            foreach (var metadata in featureResult.Metadata)
            {
                writer.WriteStartElement("Property");
                writer.WriteElementString("Key", metadata.Key);
                writer.WriteElementString("Value", metadata.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        foreach (var scenarioResult in featureResult.ScenarioResults)
        {
            WriteScenario(writer, scenarioResult.Value);
        }

        writer.WriteEndElement();
    }

    private void WriteScenario(XmlWriter writer, ScenarioFeatureResult scenarioResult)
    {
        writer.WriteStartElement("Scenario");

        writer.WriteElementString("Name", scenarioResult.Name);
        writer.WriteElementString("Id", scenarioResult.Id);

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

    private void WriteIteration(XmlWriter writer, IterationFeatureResult iterationResult)
    {
        writer.WriteStartElement("Iteration");

        writer.WriteElementString("Status", iterationResult.Passed ? "Passed" : "Failed");

        if (iterationResult.InputData is not null)
            writer.WriteElementString("InputData", iterationResult.InputData);

        WriteSteps(writer, iterationResult.StepResults.Values.ToList());

        writer.WriteEndElement();
    }

    private void WriteSteps(XmlWriter writer, List<StepFeatureResult> steps)
    {
        writer.WriteStartElement("Steps");
        foreach (var step in steps)
        {
            WriteStep(writer, step);
        }
        writer.WriteEndElement();
    }

    private void WriteStep(XmlWriter writer, StepFeatureResult step)
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
