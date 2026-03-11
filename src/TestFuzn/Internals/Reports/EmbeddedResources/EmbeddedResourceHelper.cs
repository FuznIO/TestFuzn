namespace Fuzn.TestFuzn.Internals.Reports.EmbeddedResources;

internal static class EmbeddedResourceHelper
{
    /// <summary>
    /// Extracts an embedded resource from the executing assembly and writes it to the given output path.
    /// </summary>
    /// <param name="fileSystem">File system abstraction used for directory and file operations.</param>
    /// <param name="resourceName">The full embedded resource name (e.g., "YourNamespace.Assets.Scripts.report.js").</param>
    /// <param name="outputPath">The full file path to write the resource to (e.g., "TestResults/my-report/report.js").</param>
    public static async Task WriteEmbeddedResourceToFile(IFileSystem fileSystem, string resourceName, string outputPath)
    {
        var assembly = fileSystem.GetType().Assembly;
        await using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            fileSystem.CreateDirectory(outputDir);

        await using var fileStream = fileSystem.CreateFile(outputPath);
        await resourceStream.CopyToAsync(fileStream);
    }
}
