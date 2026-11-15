namespace Fuzn.TestFuzn.Tests;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var exitCode = await TestFuznHost.Run<Startup>(args);

        // The host runs IAfterSuite after the last test, so no test can check it.
        if (Startup.BeforeSuiteExecuted && !Startup.AfterSuiteExecuted)
        {
            Console.Error.WriteLine("IAfterSuite.AfterSuite was not called.");
            return 1;
        }

        return exitCode;
    }
}
