using Microsoft.Extensions.Configuration;

namespace Fuzn.TestFuzn.Internals.AppConfiguration;

internal class ConfigurationLoader : IConfigurationLoader
{
    private static readonly object _configLocker = new();

    public IConfigurationRoot LoadConfigRoot(string executionEnvironment, string targetEnvironment, string nodeName)
    {
        lock (_configLocker)
        {
            var basePath = Directory.GetCurrentDirectory();
            var builder = new ConfigurationBuilder()
                                .SetBasePath(basePath)
                                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(executionEnvironment))
                builder.AddJsonFile(FindFile(basePath, $"appsettings.exec-{executionEnvironment}.json"), optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(targetEnvironment))
                builder.AddJsonFile(FindFile(basePath, $"appsettings.target-{targetEnvironment}.json"), optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(executionEnvironment) && !string.IsNullOrEmpty(targetEnvironment))
                builder.AddJsonFile(FindFile(basePath, $"appsettings.exec-{executionEnvironment}.target-{targetEnvironment}.json"), optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(nodeName))
                builder.AddJsonFile(FindFile(basePath, $"appsettings.{nodeName}.json"), optional: true, reloadOnChange: false);

            var configRoot = builder.Build();

            return configRoot;
        }
    }

    // --target-environment=Staging must find appsettings.target-staging.json on Linux too, where file
    // names are case sensitive; on Windows and macOS it already does.
    internal static string FindFile(string basePath, string fileName)
    {
        if (File.Exists(Path.Combine(basePath, fileName)) || !Directory.Exists(basePath))
            return fileName;

        var match = Directory.EnumerateFiles(basePath, "appsettings*.json")
            .Select(Path.GetFileName)
            .FirstOrDefault(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase));

        if (match == null)
            return fileName;

        return match;
    }
}
