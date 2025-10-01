using Spectre.Console;

namespace Fuzn.TestFuzn.Cli.Internals;

internal class ScenarioTestRunner
{
    internal async Task RunTest(ScenarioTestInfo testInfo)
    {
        AnsiConsole.Write(new Markup($"[green]Running test:[/] [bold green]{testInfo.Name}[/]"));
        AnsiConsole.WriteLine();

        var testClassInstance = Activator.CreateInstance(testInfo.Class);
        var testFramework = new TestFuznProvider();
        if (testClassInstance == null)
        {
            AnsiConsole.Write(new Markup($"[red]Could not create instance of test class '{testInfo.Class.Name}'.[/]"));
            AnsiConsole.WriteLine();
            return;
        }

        try
        {
            await TestFuznIntegration.InitGlobal(testFramework);

            var invocationResult = testInfo.Method.Invoke(testClassInstance, null);

            if (invocationResult is Task task)
                await task;
        }
        finally
        {
            await TestFuznIntegration.CleanupGlobal(testFramework);
        }
    }
}
