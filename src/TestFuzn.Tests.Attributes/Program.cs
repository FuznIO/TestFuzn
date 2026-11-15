namespace Fuzn.TestFuzn.Tests.Attributes;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var exitCode = await TestFuznHost.Run<Startup>(args);

        // The host writes the reports after the last test, so no test can check that this one ran.
        if (Startup.SuiteStarted && !AssertStandardReport.IsExecuted)
        {
            Console.Error.WriteLine("The standard report was not written.");
            return 1;
        }

        return exitCode;
    }
}
