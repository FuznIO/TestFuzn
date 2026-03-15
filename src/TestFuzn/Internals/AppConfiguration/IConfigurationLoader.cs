using Microsoft.Extensions.Configuration;

namespace Fuzn.TestFuzn.Internals.AppConfiguration;

internal interface IConfigurationLoader
{
    /// <summary>
    /// Loads the configuration root based on the current test session context.
    /// </summary>
    IConfigurationRoot LoadConfigRoot(string executionEnvironment, string targetEnvironment, string nodeName);
}
