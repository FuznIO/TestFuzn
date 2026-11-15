namespace Fuzn.TestFuzn.Tests.Attributes;

public class Startup : IStartup, IBeforeSuite
{
    public static bool SuiteStarted { get; private set; }

    public void Configure(TestFuznConfiguration configuration)
    {
        configuration.AddStandardReport(new AssertStandardReport());
    }

    public Task BeforeSuite(Context context)
    {
        SuiteStarted = true;
        return Task.CompletedTask;
    }
}
