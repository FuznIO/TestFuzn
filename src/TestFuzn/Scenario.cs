using Fuzn.TestFuzn.Contracts;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.InputData;

namespace Fuzn.TestFuzn;

internal class Scenario
{
    public string Name { get; internal set; }
    public string Id { get; internal set; }
    internal System.Collections.Concurrent.ConcurrentDictionary<string, string> StepIds { get; } = new();

    internal void EnsureStepIdIsUnique(Step step)
    {
        var parentId = "";
        if (step.ParentId != null)
            parentId = step.ParentId;

        var key = StableId.Key(parentId, step.Name);
        var existing = StepIds.GetOrAdd(step.Id, key);
        if (existing != key)
        {
            var otherName = existing.Substring(existing.LastIndexOf('') + 1);
            throw new InvalidOperationException($"The steps '{otherName}' and '{step.Name}' in scenario '{Name}' have the same id '{step.Id}'. Every step in a scenario needs its own id.");
        }
    }
    public string Description { get; internal set; }
    internal TestType TestType
    {
        get
        {
            if (SimulationsAction == null)
                return TestType.Standard;
            return TestType.Load;
        }
    }
    internal Type ContextType { get; set; }
    internal Func<ScenarioContext, Task> BeforeScenario { get; set; }
    internal Func<IterationContext, Task> BeforeIterationAction { get; set; }
    internal Func<IterationContext, Task> AfterIterationAction { get; set; }
    internal Func<ScenarioContext, Task> AfterScenarioAction { get; set; }
    internal InputDataInfo InputDataInfo { get; private set; } = new();
    internal List<Step> Steps { get; } = new();
    internal Func<ScenarioContext, SimulationsBuilder, Task> WarmupAction;
    internal Func<ScenarioContext, SimulationsBuilder, Task> SimulationsAction;
    internal List<ILoadConfiguration> SimulationsInternal { get; } = new();
    internal Action<ScenarioContext, WarmupStats>? AssertWhileWarmingUpAction;
    internal Action<ScenarioContext, AssertScenarioStats>? AssertWhileRunningAction;
    internal Action<ScenarioContext, AssertScenarioStats>? AssertWhenDoneAction;
    internal Func<ScenarioContext, Task<object?>>? BeforeScenarioOnceAction;
    internal Func<ScenarioContext, Task>? AfterScenarioOnceAction;

    // What BeforeScenarioOnce returned, as JSON. A cluster sends it to every load worker, and a single
    // node round-trips it too, so serialization problems show up locally first.
    internal string? OnceDataJson { get; set; }

    // Read by ScenarioRunGuard to find scenarios that were built but never awaited.
    internal bool IsUsed { get; set; }
    internal bool HasBuildError { get; set; }

    public Scenario()
    {

    }
    public Scenario(string name)
    {
        Name = name;
        SimulationsInternal = new();
    }
}
