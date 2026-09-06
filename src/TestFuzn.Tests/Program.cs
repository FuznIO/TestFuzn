namespace Fuzn.TestFuzn.Tests;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return TestFuznHost.Run<Startup>(args);
    }
}
