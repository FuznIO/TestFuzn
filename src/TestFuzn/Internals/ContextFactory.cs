using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals;

internal class ContextFactory
{
    public static Context CreateContext(ITestFrameworkAdapter testFramework, string stepName)
    {
        var context = new Context();

        if (GlobalState.Configuration != null)
        {
            context.IterationState = new();
            PopulateSharedProperties(testFramework, context.IterationState);
            context.StepInfo = new StepInfo(null, stepName, null, null);
        }

        return context;
    }

    public static ScenarioContext CreateScenarioContext(ITestFrameworkAdapter testFramework, string stepName)
    {
        var context = new ScenarioContext();

        if (GlobalState.Configuration != null)
        {
            context.IterationState = new();
            PopulateSharedProperties(testFramework, context.IterationState);
            context.StepInfo = new StepInfo(null, stepName, null, null);
        }

        return context;
    }

    public static IterationContext CreateIterationContext(IterationState iterationState, string stepName, string stepId, string parentName)
    {
        var context = (IterationContext) Activator.CreateInstance(iterationState.Scenario.ContextType);
        if (context == null)
            throw new InvalidOperationException($"Failed to create instance of {iterationState.Scenario.ContextType}");

        context.IterationState = iterationState;
        context.StepInfo = new StepInfo(context, stepName, stepId, parentName);

        return context;
    }

    public static IterationState CreateIterationState(ITestFrameworkAdapter testFramework, Scenario scenario, object currentInput)
    {
        var state = new IterationState();
        if (scenario.ContextType.IsGenericType && scenario.ContextType.GetGenericTypeDefinition() == typeof(IterationContext<>))
        {
            var modelType = scenario.ContextType.GetGenericArguments()[0];
            var modelInstance = Activator.CreateInstance(modelType);

            if (modelInstance == null)
                throw new InvalidOperationException($"Failed to create instance of {modelType}");

            state.Model = modelInstance;
        }
        PopulateSharedProperties(testFramework, state);
        state.SharedData = new();
        state.Scenario = scenario;
        state.InputData = currentInput;

        return state;
    }

    private static void PopulateSharedProperties(ITestFrameworkAdapter testFramework, IterationState context)
    {
        context.Info = new ExecutionInfo();
        context.Info.EnvironmentName = GlobalState.Configuration.EnvironmentName;
        context.Info.NodeName = GlobalState.NodeName;
        context.Info.TestRunId = GlobalState.TestRunId;
        context.Info.CorrelationId = Guid.NewGuid().ToString();
        context.Logger = GlobalState.Logger;
        context.TestFramework = testFramework;
        context.Internals = new ContextInternals();
        
        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
        {
            if (!plugin.RequireState)
                continue;

            if (context.Internals.Plugins == null)
                context.Internals.Plugins = new ContextPluginsState();

            var state = plugin.InitContext();
            context.Internals.Plugins.SetState(plugin.GetType(), state);
        }
    }
}
