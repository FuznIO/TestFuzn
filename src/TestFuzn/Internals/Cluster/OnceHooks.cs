using System.Text.Json;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Cluster;

// .Load().BeforeScenarioOnce / AfterScenarioOnce: on one machine they run in the test process, in a cluster
// run on the controller. Either way the data goes through JSON.
internal static class OnceHooks
{
    public static async Task RunBefore(TestExecutionState state, IServiceProvider serviceProvider)
    {
        foreach (var scenario in state.Scenarios)
        {
            if (scenario.BeforeScenarioOnceAction == null)
                continue;

            var context = ContextFactory.CreateScenarioContext(state.TestSession, serviceProvider, state.TestFramework, "BeforeScenarioOnce", state.CancellationToken, scenario);
            var data = await scenario.BeforeScenarioOnceAction(context);
            scenario.OnceDataJson = Serialize(scenario, data);
        }
    }

    public static async Task RunAfter(TestExecutionState state, IServiceProvider serviceProvider)
    {
        foreach (var scenario in state.Scenarios)
        {
            if (scenario.AfterScenarioOnceAction == null)
                continue;

            var context = ContextFactory.CreateScenarioContext(state.TestSession, serviceProvider, state.TestFramework, "AfterScenarioOnce", CancellationToken.None, scenario);
            await scenario.AfterScenarioOnceAction(context);
        }
    }

    public static void ApplyOnceData(TestExecutionState state)
    {
        foreach (var scenario in state.Scenarios)
        {
            if (state.TestSession.Cluster.OnceData.TryGetValue(scenario.Name, out var json))
                scenario.OnceDataJson = json;
        }
    }

    private static string Serialize(Scenario scenario, object? data)
    {
        if (data == null)
            return "null";

        try
        {
            return JsonSerializer.Serialize(data, data.GetType(), ClusterJson.Options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"BeforeScenarioOnce of scenario '{scenario.Name}' returned data that cannot be serialized to JSON. Return plain data objects.", ex);
        }
    }
}
