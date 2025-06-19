using TestFusion.Cli;

namespace TestFusion.Tests.Runner;

internal class Program
{
    static async Task Main(string[] args)
    {
        await new CliTestRunner().Run(typeof(TestFusion.Tests.Startup).Assembly, args);
    }
}