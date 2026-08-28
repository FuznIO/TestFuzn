namespace Fuzn.TestFuzn.Tests.StandaloneRunner;

/// <summary>A startup that configures nothing — the runner core's startup type in tests whose runs never initialize a session.</summary>
internal sealed class FakeStartup : IStartup
{
    public void Configure(TestFuznConfiguration configuration)
    {
    }
}
