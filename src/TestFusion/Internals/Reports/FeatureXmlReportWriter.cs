using System.Text;
using System.Xml;
using TestFusion.Contracts.Reports;
using TestFusion.Contracts.Results.Feature;

namespace TestFusion.Internals.Reports;

internal class FeatureXmlReportWriter : IFeatureReport
{
    public async Task WriteReport(FeatureReportData featureReportData)
    {
        try
        {
            var filePath = Path.Combine(featureReportData.TestsOutputDirectory, "TestFusion_Report_Features.xml");

            var stringBuilder = new StringBuilder();
            using (var writer = XmlWriter.Create(stringBuilder, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("TestRunResults");
                writer.WriteAttributeString("version", "1.0");

                writer.WriteElementString("TestSuiteName", featureReportData.TestSuiteName);
                writer.WriteElementString("TestRunId", featureReportData.TestRunId);

                foreach (var featureResult in featureReportData.Results.FeatureResults.Values)
                {
                    WriteFeature(writer, featureResult);
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            await File.WriteAllTextAsync(filePath, stringBuilder.ToString());
        }
        catch (Exception ex)
        {
            // Log or handle the exception as needed
            throw new InvalidOperationException("Failed to write XML report.", ex);
        }
    }

    private void WriteFeature(XmlWriter writer, FeatureResult featureResult)
    {
        writer.WriteStartElement("Feature");
        writer.WriteAttributeString("name", featureResult.Name);

        foreach (var scenarioResult in featureResult.ScenarioResults)
        {
            WriteScenario(writer, scenarioResult);
        }

        writer.WriteEndElement();
    }

    private void WriteScenario(XmlWriter writer, ScenarioFeatureResult scenarioResult)
    {
        writer.WriteStartElement("Scenario");
        writer.WriteAttributeString("name", scenarioResult.Name);
        writer.WriteAttributeString("status", scenarioResult.Status == ScenarioStatus.Passed ? "Passed" : "Failed");

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
                WriteSteps(writer, iterationResult);
        }

        writer.WriteEndElement();
    }

    private void WriteIteration(XmlWriter writer, IterationFeatureResult iterationResult)
    {
        writer.WriteStartElement("Iteration");
        writer.WriteAttributeString("status", iterationResult.Passed ? "Passed" : "Failed");

        writer.WriteElementString("InputData", iterationResult.InputData);

        WriteSteps(writer, iterationResult);

        writer.WriteEndElement();
    }

    private void WriteSteps(XmlWriter writer, IterationFeatureResult iterationResult)
    {
        writer.WriteStartElement("Steps");
        foreach (var step in iterationResult.StepResults)
        {
            writer.WriteStartElement("Step");
            writer.WriteAttributeString("name", step.Value.Name);
            writer.WriteAttributeString("status", step.Value.Status.ToString());
            writer.WriteAttributeString("duration", step.Value.Duration.ToString(@"hh\:mm\:ss\.fff"));

            if (step.Value.Attachments != null && step.Value.Attachments.Count > 0)
            {
                writer.WriteStartElement("Attachments");
                foreach (var attachment in step.Value.Attachments)
                {
                    writer.WriteStartElement("Attachment");
                    writer.WriteAttributeString("name", attachment.Name);
                    writer.WriteAttributeString("path", attachment.Path);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();


        }
        writer.WriteEndElement();
    }
}
