using Microsoft.Extensions.Configuration;

namespace Fuzn.TestFuzn;

public class ConfigurationManager
{
    private static IConfigurationRoot _configRoot;
    private static readonly object _locker = new();

    /// <summary>
    /// Returns true if the configuration section TestFuzn:{sectionName} exists. Otherwise, false.
    /// </summary>
    public static bool HasSection(string sectionName)
    {
        try
        {
            var section = GetConfigRoot().GetSection("TestFuzn").GetSection(sectionName);

            return section.Exists();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the configuration section from TestFuzn:{sectionName} as the specified type. Throws an exception if not found.
    /// </summary>
    public static T GetRequiredSection<T>(string sectionName) 
        where T : new()
    {
        try
        {
            var section = GetConfigRoot().GetSection("TestFuzn").GetSection(sectionName);
            if (!section.Exists())
            {
                throw new InvalidOperationException(@$"Failed to load configuration for section '{sectionName}'. 
                    Ensure that the configuration section exists in appsettings.json.");
            }

            var instance = new T();
            section.Bind(instance);
            return instance;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
           throw new InvalidOperationException(@$"Failed to load configuration for section '{sectionName}'. 
                Ensure that the configuration section exists in appsettings.json.", ex);
        }
    }

    /// <summary>
    /// Returns true if the value exists in TestFuzn:Values:{key}. Otherwise, false.
    /// </summary>
    public static bool HasValue(string key)
    {
        try
        {
            var section = GetConfigRoot().GetSection($"TestFuzn:Values:{key}");
            return section.Exists();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the value from TestFuzn:Values:{key} as the specified type. Throws an exception if not found or cannot be converted.
    /// </summary>    
    public static T GetRequiredValue<T>(string key)
    {
        var section = GetConfigRoot().GetSection($"TestFuzn:Values:{key}");
        if (!section.Exists())
            throw new KeyNotFoundException($"Configuration key 'TestFuzn:Values:{key}' not found.");

        try
        {
            var value = section.Get<T>();

            return value;
        }
        catch
        {
            throw new InvalidOperationException($"Configuration key 'TestFuzn:Values:{key}' could not be converted to type {typeof(T).Name}.");
        }
    }
    private static IConfigurationRoot GetConfigRoot()
    {
        if (_configRoot != null)
            return _configRoot;

        lock (_locker)
        {
            if (_configRoot != null)
                return _configRoot;

            var builder = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

            var executionEnv = GlobalState.ExecutionEnvironment;
            var targetEnv = GlobalState.TargetEnvironment;
            var nodeName = GlobalState.NodeName;

            if (!string.IsNullOrEmpty(executionEnv))
                builder.AddJsonFile($"appsettings.exec-{executionEnv}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(targetEnv))
                builder.AddJsonFile($"appsettings.target-{targetEnv}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(executionEnv) && !string.IsNullOrEmpty(targetEnv))
                builder.AddJsonFile($"appsettings.exec-{executionEnv}.target-{targetEnv}.json", optional: true, reloadOnChange: false);

            if (!string.IsNullOrEmpty(nodeName))
                builder.AddJsonFile($"appsettings.{nodeName}.json", optional: true, reloadOnChange: false);

            _configRoot = builder.Build();

            return _configRoot;
        }
    }
}
