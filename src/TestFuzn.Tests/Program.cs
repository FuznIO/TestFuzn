using Microsoft.Testing.Platform.Builder;

namespace Fuzn.TestFuzn.Tests;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (TestFuznHost.IsStandaloneRun(args))
            return await TestFuznHost.RunStandalone<Startup>(typeof(Program).Assembly, args);

        var builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddSelfRegisteredExtensions(args);
        using var app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}
