using Microsoft.Extensions.Configuration;
using System.Reflection;
using TestFusion.Internals.State;

namespace TestFusion;

public class ConfigurationManager
{
    private static IConfigurationRoot _configRoot;

    public static TestFusionConfiguration LoadConfiguration()
    {
        var config = new TestFusionConfiguration();
        config.EnvironmentName = "dev";
        config.TestSuiteName = GlobalState.AssemblyWithTestsName;
        if (config.TestSuiteName == null)
            config.TestSuiteName = Assembly.GetExecutingAssembly().GetName().Name;
        GetConfigRoot().Bind("TestFusion", config);

        return config;
    }

    public static void LoadPluginConfiguration(string pluginName, object configInstance)
    {
        try
        {
            GetConfigRoot().GetSection("TestFusion").Bind(pluginName, configInstance);
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

        _configRoot = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                            .AddJsonFile($"appsettings.{machineName}.json", optional: true, reloadOnChange: false)
                            .Build();

        return _configRoot;
    }
}
