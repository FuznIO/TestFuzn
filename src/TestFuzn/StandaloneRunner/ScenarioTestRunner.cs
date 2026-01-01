using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;
using Spectre.Console;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class ScenarioTestRunner
{
    internal async Task RunScenarioTest(string[] args, ITestFrameworkAdapter testFramework, 
        ScenarioTestInfo testInfo)
    {
        AnsiConsole.Write(new Markup($"[green]Running test:[/] [bold green]{testInfo.Name}[/]"));
        AnsiConsole.WriteLine();

        var testClassInstance = Activator.CreateInstance(testInfo.Class);
        var featureTestClassInstance = testClassInstance as ITest;
        if (featureTestClassInstance == null)
            throw new Exception($"Test class '{testInfo.Class.Name}' must implement IFeatureTest interface.");

        if (testClassInstance == null)
        {
            AnsiConsole.Write(new Markup($"[red]Could not create instance of test class '{testInfo.Class.Name}'.[/]"));
            AnsiConsole.WriteLine();
            return;
        }

        try
        {
            await TestFuznIntegrationCore.InitGlobal(testFramework);
            featureTestClassInstance.TestFramework = testFramework;
            featureTestClassInstance.TestMethodInfo = testInfo.Method;

            var invocationResult = testFramework.ExecuteTestMethod(featureTestClassInstance, testInfo.Method);

            if (invocationResult is Task task)
                await task;
        }
        finally
        {
            await TestFuznIntegrationCore.CleanupGlobal(testFramework);
        }
    }
}
