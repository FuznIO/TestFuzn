using System.Reflection;

namespace TestFusion.Internals.Reports.EmbeddedResources;

public static class EmbeddedResourceHelper
{
    /// <summary>
    /// Extracts an embedded resource from the executing assembly and writes it to the given output path.
    /// </summary>
    /// <param name="resourceName">The full embedded resource name (e.g., "YourNamespace.Assets.Scripts.report.js").</param>
    /// <param name="outputPath">The full file path to write the resource to (e.g., "TestResults/my-report/report.js").</param>
    public static async Task WriteEmbeddedResourceToFile(string resourceName, string outputPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        await using var fileStream = File.Create(outputPath);
        await resourceStream.CopyToAsync(fileStream);
    }
}
