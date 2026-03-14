using Microsoft.Extensions.Configuration;

namespace Fuzn.TestFuzn.Internals.FileConfiguration;

internal class ConfigurationLoader : IConfigurationLoader
{
    private static readonly object _configLocker = new();

    public IConfigurationRoot LoadConfigRoot(string executionEnvironment, string targetEnvironment, string nodeName)
    {
        lock (_configLocker)
        {
            var builder = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
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
