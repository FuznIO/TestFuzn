using Fuzn.TestFuzn.Adapters;
using Fuzn.TestFuzn.StandaloneRunner;
using System.Reflection;

namespace Fuzn.TestFuzn;

public class TestFuznStandaloneRunner
{
    public async Task Run(Assembly assembly, 
        string[] args)
    {
        var cli = new StandaloneRunnerCore();
        await cli.Run(assembly, args, () => new StandaloneRunnerAdapter());
    }
}
