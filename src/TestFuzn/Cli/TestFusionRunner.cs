using System.Reflection;
using System.Text;
using FuznLabs.TestFuzn.Cli.Internals;
using FuznLabs.TestFuzn.Internals.State;

namespace FuznLabs.TestFuzn.Cli;

public class CliTestRunner
{
    public async Task Run(Assembly assembly, string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        GlobalState.CustomTestRunner = true;
        GlobalState.AssemblyWithTestsName = assembly.GetName().Name;

        var scenarioTests = new DiscoverScenarioTests().GetScenarioTests(assembly);

        var scenarioTestName = "";

        if (args.Length == 0)
        {
            scenarioTestName = new TestSelectionMenu().DisplayAndSelectTest(scenarioTests);

            if (scenarioTestName == null)
                return;
        }
        else
            scenarioTestName = args[0];

        var testInfo = scenarioTests.SingleOrDefault(t => t.Name == scenarioTestName);
        if (testInfo == null)
        {
            Console.WriteLine($"Test '{scenarioTestName}' not found.");
            return;
        }

        await new ScenarioTestRunner().RunTest(testInfo);
    }
}
