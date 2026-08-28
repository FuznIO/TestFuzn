using Microsoft.Extensions.Configuration;

namespace Fuzn.TestFuzn.Internals.AppConfiguration;

internal class ConfigurationLoader : IConfigurationLoader
{
    private static readonly object _configLocker = new();

    public IConfigurationRoot LoadConfigRoot(string executionEnvironment, string targetEnvironment, string nodeName)
    {
        lock (_configLocker)
        {
            // Base path is the test assembly's output directory, where appsettings files
            // are copied to. The current directory is not reliable: it depends on where
            // the test executable was launched from (e.g. "dotnet run" from a repo root).
            var builder = new ConfigurationBuilder()
                                .SetBasePath(AppContext.BaseDirectory)
                                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(executionEnvironment))
                builder.AddJsonFile($"appsettings.exec-{executionEnvironment}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(targetEnvironment))
                builder.AddJsonFile($"appsettings.target-{targetEnvironment}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(executionEnvironment) && !string.IsNullOrEmpty(targetEnvironment))
                builder.AddJsonFile($"appsettings.exec-{executionEnvironment}.target-{targetEnvironment}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(nodeName))
                builder.AddJsonFile($"appsettings.{nodeName}.json", optional: true, reloadOnChange: false);

            var configRoot = builder.Build();

            return configRoot;
        }
    }
}
