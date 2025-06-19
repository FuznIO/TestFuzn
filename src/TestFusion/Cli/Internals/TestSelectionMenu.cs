using Spectre.Console;

namespace TestFusion.Cli.Internals;

internal class TestSelectionMenu
{
    public string DisplayAndSelectTest(List<ScenarioTestInfo> scenarioTests)
    {
        var testTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.SeaGreen1)
            .AddColumn(new TableColumn("ID").Centered())
            .AddColumn(new TableColumn("Test Name"));

        AnsiConsole.Write(new Markup("[bold green]TestFusion Test Runner[/]"));
        AnsiConsole.WriteLine();
        
        var i = 0;
        foreach (var test in scenarioTests)
        {
            i++;
            testTable.AddRow($"{i}", $"{test.Name}");
        }

        AnsiConsole.Write(testTable);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Markup("[bold green]Enter the ID of the test you want to run or enter to quit:[/]"));
        AnsiConsole.WriteLine();
        
        while (true)
        {
            var testIndex = Console.ReadLine();

            if (string.IsNullOrEmpty(testIndex))
                return null;

            if (int.TryParse(testIndex, out int index))
            {
                index--;

                if (index >= 0 && index < scenarioTests.Count)
                    return scenarioTests[index].Name;    
            }

            AnsiConsole.Write(new Markup("[bold red]Invalid test index.[/]"));
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Markup("[bold green]Enter the ID of the test you want to run or enter to quit:[/]"));
            AnsiConsole.WriteLine();
        }
    }
}
