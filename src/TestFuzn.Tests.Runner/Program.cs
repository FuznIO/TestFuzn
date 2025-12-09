namespace Fuzn.TestFuzn.Tests.Runner;

public class Program
{
    static async Task Main(string[] args)
    {
        await new TestFuznStandaloneRunner().Run(typeof(TestFuzn.Tests.Startup).Assembly, args);
    }
}