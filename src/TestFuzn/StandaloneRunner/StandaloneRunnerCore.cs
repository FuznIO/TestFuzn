using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;
using System.Reflection;
using System.Text;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class StandaloneRunnerCore
{
    public async Task Run(Assembly assembly, 
        string[] args, Func<ITestFrameworkAdapter> testFrameworkInstanceCreator)
    {
        var parsedArgs = ArgumentsParser.Parse(args);

        Console.OutputEncoding = Encoding.UTF8;
        GlobalState.AssemblyWithTestsName = assembly.GetName().Name;

        var scenarioTests = new DiscoverScenarioTests().GetScenarioTests(assembly);

        var scenarioTestName = ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, "test-name", "TESTFUZN_TEST_NAME");

        if (string.IsNullOrEmpty(scenarioTestName))
        {
            scenarioTestName = new TestSelectionMenu().DisplayAndSelectTest(scenarioTests);

            if (scenarioTestName == null)
                return;
        }

        var testInfo = scenarioTests.SingleOrDefault(t => t.Name == scenarioTestName);
        if (testInfo == null)
        {
            Console.WriteLine($"Test '{scenarioTestName}' not found.");
            return;
        }

        await new ScenarioTestRunner().RunScenarioTest(args, testFrameworkInstanceCreator(), testInfo);
    }
}
