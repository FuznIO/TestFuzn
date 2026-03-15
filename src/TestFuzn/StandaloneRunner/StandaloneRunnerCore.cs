using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;
using System.Reflection;
using System.Text;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class StandaloneRunnerCore
{
    public async Task Run<TStartup>(Assembly assembly, 
        string[] args, Func<ITestFrameworkAdapter> testFrameworkInstanceCreator)
        where TStartup : IStartup, new()
    {
        var argumentsParser = new ArgumentsParser(new EnvironmentWrapper());
        var parsedArgs = argumentsParser.Parse(args);

        Console.OutputEncoding = Encoding.UTF8;

        var tests = new DiscoverTests().GetTests(assembly);

        var testName = argumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, "test-name", "TESTFUZN_TEST_NAME");

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

        await new StandaloneTestRunner().RunTest<TStartup>(args, testFrameworkInstanceCreator(), testInfo);
    }
}
