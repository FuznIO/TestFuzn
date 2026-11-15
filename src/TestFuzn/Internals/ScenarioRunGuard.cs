namespace Fuzn.TestFuzn.Internals;

// A scenario that is built but never awaited does not run, and the test would pass without it.
internal static class ScenarioRunGuard
{
    internal static void Track(TestInfo testInfo, Scenario scenario)
    {
        lock (testInfo.BuiltScenarios)
            testInfo.BuiltScenarios.Add(scenario);
    }

    internal static string? GetNotRunError(TestInfo? testInfo)
    {
        if (testInfo == null)
            return null;

        List<string> notRunNames;
        lock (testInfo.BuiltScenarios)
            notRunNames = testInfo.BuiltScenarios.Where(s => !s.IsUsed && !s.HasBuildError).Select(s => s.Name != null ? s.Name : testInfo.Name).ToList();

        if (notRunNames.Count == 0)
            return null;

        var names = string.Join("', '", notRunNames);
        return $"Scenario '{names}' was built but never run. Add 'await' before Scenario()";
    }

    internal static void ThrowIfNotRun(TestInfo? testInfo)
    {
        var error = GetNotRunError(testInfo);
        if (error != null)
            throw new InvalidOperationException(error);
    }
}
