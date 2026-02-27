using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals;
using Spectre.Console;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class StandaloneTestRunner
{
    internal async Task RunTest(string[] args, ITestFrameworkAdapter testFramework, 
        DiscoveredTest testInfo)
    {
        AnsiConsole.Write(new Markup($"[green]Running test:[/] [bold green]{testInfo.Name}[/]"));
        AnsiConsole.WriteLine();

        var testClassInstance = Activator.CreateInstance(testInfo.Class);
        var iTestClassInstance = testClassInstance as ITest;
        if (iTestClassInstance == null)
            throw new Exception($"Test class '{testInfo.Class.Name}' must implement {nameof(ITest)} interface.");

        if (testClassInstance == null)
        {
            AnsiConsole.Write(new Markup($"[red]Could not create instance of test class '{testInfo.Class.Name}'.[/]"));
            AnsiConsole.WriteLine();
            return;
        }

        try
        {
            await TestFuznIntegrationCore.Init(testFramework);
            iTestClassInstance.TestFramework = testFramework;
            iTestClassInstance.TestMethodInfo = testInfo.Method;

            var invocationResult = testFramework.ExecuteTestMethod(iTestClassInstance, testInfo.Method);

            if (invocationResult is Task task)
                await task;
        }
        finally
        {
            await TestFuznIntegrationCore.Cleanup(testFramework);
        }
    }
}
