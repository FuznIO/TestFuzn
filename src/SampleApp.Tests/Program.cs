using Fuzn.TestFuzn;

namespace SampleApp.Tests;

internal static class Program
{
    public static Task<int> Main(string[] args) => TestFuznHost.Run<Startup>(args);
}
