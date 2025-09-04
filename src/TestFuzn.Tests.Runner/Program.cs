using Fuzn.TestFuzn.Cli;

namespace Fuzn.TestFuzn.Tests.Runner;

internal class Program
{
    static async Task Main(string[] args)
    {
        await new CliTestRunner().Run(typeof(TestFuzn.Tests.Startup).Assembly, args);
    }
}