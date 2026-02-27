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

        var tests = new DiscoverTests().GetTests(assembly);

        var testName = ArgumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, "test-name", "TESTFUZN_TEST_NAME");

        if (string.IsNullOrEmpty(testName))
        {
            testName = new TestSelectionMenu().DisplayAndSelectTest(tests);

            if (testName == null)
                return;
        }

        var testInfo = tests.SingleOrDefault(t => t.Name == testName);
        if (testInfo == null)
        {
            Console.WriteLine($"Test '{testName}' not found.");
            return;
        }

        await new StandaloneTestRunner().RunTest(args, testFrameworkInstanceCreator(), testInfo);
    }
}
