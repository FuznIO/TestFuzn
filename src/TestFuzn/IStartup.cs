namespace Fuzn.TestFuzn;

/// <summary>
/// Interface for configuring TestFuzn at startup.
/// Implement this interface in your test assembly to customize TestFuzn settings.
/// </summary>
public interface IStartup
{
    /// <summary>
    /// Configures the TestFuzn framework settings.
    /// </summary>
    /// <param name="configuration">The configuration object to modify.</param>
    public void Configure(TestFuznConfiguration configuration);
}