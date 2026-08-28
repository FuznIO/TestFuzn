using Fuzn.TestFuzn.Contracts.Adapters;
using Spectre.Console;
using System.Reflection;
using System.Text;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class StandaloneRunnerCore
{
    public async Task<int> Run<TStartup>(Assembly testAssembly,
        string[] args, Func<ITestFrameworkAdapter> testFrameworkInstanceCreator)
        where TStartup : IStartup, new()
    {
        var argumentsParser = new ArgumentsParser(new EnvironmentWrapper());
        var parsedArgs = argumentsParser.Parse(args);

        Console.OutputEncoding = Encoding.UTF8;

        var tests = new DiscoverTests().GetTests(testAssembly);

        var testName = argumentsParser.GetValueFromArgsOrEnvironmentVariable(parsedArgs, "test-name", "TESTFUZN_TEST_NAME");

        if (string.IsNullOrEmpty(testName))
        {
            testName = new TestSelectionMenu().DisplayAndSelectTest(tests);

            if (testName == null)
                return 0;
        }

        var testInfo = tests.SingleOrDefault(t => t.Name == testName);
        if (testInfo == null)
        {
            Console.WriteLine($"Test '{testName}' not found.");
            return 1;
        }

        var adapter = testFrameworkInstanceCreator();
        try
        {
            await new StandaloneTestRunner().RunTest<TStartup>(args, adapter, testInfo);
            return 0;
        }
        catch (Exception ex) when (IsScenarioRunModeIgnore(ex))
        {
            AnsiConsole.MarkupLine("[yellow]Test skipped.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            (adapter as IDisposable)?.Dispose();
        }
    }

    private static bool IsScenarioRunModeIgnore(Exception ex)
    {
        if (ex is ScenarioRunModeIgnoreException)
            return true;

        if (ex is TargetInvocationException invocationException && invocationException.InnerException is ScenarioRunModeIgnoreException)
            return true;

        return false;
    }
}
