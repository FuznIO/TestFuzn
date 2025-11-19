using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Fuzn.TestFuzn;

public class ConfigurationManager
{
    private static IConfigurationRoot _configRoot;

    public static TestFuznConfiguration LoadConfiguration()
    {
        var config = new TestFuznConfiguration();
        config.TestSuite.Name = GlobalState.AssemblyWithTestsName;
        if (config.TestSuite.Name == null)
            config.TestSuite.Name = Assembly.GetExecutingAssembly().GetName().Name;
        GetConfigRoot().Bind("TestFuzn", config);

        return config;
    }

    public static void LoadPluginConfiguration(string pluginName, object configInstance)
    {
        try
        {
            GetConfigRoot().GetSection("TestFuzn").Bind(pluginName, configInstance);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(@$"Failed to load configuration for plugin '{pluginName}'.
                    Ensure that the configuration section {pluginName} exists in appsettings.json", ex);
        }
    }

    private static IConfigurationRoot GetConfigRoot()
    {
        if (_configRoot != null)
            return _configRoot;

        string machineName = GlobalState.NodeName;

        var builder = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        if (!string.IsNullOrEmpty(GlobalState.EnvironmentName))
            builder.AddJsonFile($"appsettings.{GlobalState.EnvironmentName}.json", optional: true, reloadOnChange: false);

        if (!string.IsNullOrEmpty(GlobalState.NodeName))
            builder.AddJsonFile($"appsettings.{GlobalState.NodeName}.json", optional: true, reloadOnChange: false);

        _configRoot = builder.Build();

        return _configRoot;
    }
}
