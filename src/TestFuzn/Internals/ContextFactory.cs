using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals;

internal class ContextFactory
{
    public static Context CreateContext(IServiceProvider serviceProvider, 
        ITestFrameworkAdapter testFramework, 
        string stepName)
    {
        var context = new Context();

        if (GlobalState.Configuration != null)
        {
            context.IterationState = new();
            PopulateSharedProperties(context.IterationState, serviceProvider, testFramework);
            context.StepInfo = new StepInfo(null, stepName, null, null);
        }

        return context;
    }

    public static ScenarioContext CreateScenarioContext(IServiceProvider serviceProvider, 
        ITestFrameworkAdapter testFramework, 
        string stepName)
    {
        var context = new ScenarioContext();

        if (GlobalState.Configuration != null)
        {
            context.IterationState = new();
            PopulateSharedProperties(context.IterationState, serviceProvider, testFramework);
            context.StepInfo = new StepInfo(null, stepName, null, null);
        }

        return context;
    }

    public static IterationContext CreateIterationContext(IterationState iterationState, string stepName, string? stepId, string? parentName)
    {
        var contextObj = Activator.CreateInstance(iterationState.Scenario.ContextType);
        if (contextObj is not IterationContext context)
            throw new InvalidOperationException($"Failed to create instance of {iterationState.Scenario.ContextType}");

        context.IterationState = iterationState;
        context.StepInfo = new StepInfo(context, stepName, stepId, parentName);

        return context;
    }

    public static IterationState CreateIterationState(IServiceProvider serviceProvider, 
        ITestFrameworkAdapter testFramework, 
        Scenario scenario, object? 
        currentInput)
    {
        var iterationState = new IterationState();
        if (scenario.ContextType.IsGenericType && scenario.ContextType.GetGenericTypeDefinition() == typeof(IterationContext<>))
        {
            var modelType = scenario.ContextType.GetGenericArguments()[0];
            var modelInstance = Activator.CreateInstance(modelType);

            if (modelInstance == null)
                throw new InvalidOperationException($"Failed to create instance of {modelType}");

            iterationState.Model = modelInstance;
        }
        PopulateSharedProperties(iterationState, serviceProvider, testFramework);
        iterationState.SharedData = new();
        iterationState.Scenario = scenario;
        iterationState.InputData = currentInput;

        return iterationState;
    }

    private static void PopulateSharedProperties(IterationState iterationState, IServiceProvider serviceProvider, ITestFrameworkAdapter testFramework)
    {
        iterationState.Info = new ExecutionInfo();
        iterationState.Info.TargetEnvironment = GlobalState.TargetEnvironment;
        iterationState.Info.ExecutionEnvironment = GlobalState.ExecutionEnvironment;
        iterationState.Info.NodeName = GlobalState.NodeName;
        iterationState.Info.TestRunId = GlobalState.TestRunId;
        iterationState.Info.CorrelationId = Guid.NewGuid().ToString();
        iterationState.Logger = GlobalState.Logger;
        iterationState.TestFramework = testFramework;
        iterationState.ServiceProvider = serviceProvider;
        iterationState.Internals = new ContextInternals();
        
        foreach (var plugin in GlobalState.Configuration.ContextPlugins)
        {
            if (!plugin.RequireState)
                continue;

            if (iterationState.Internals.Plugins == null)
                iterationState.Internals.Plugins = new ContextPluginsState();

            var state = plugin.InitContext(serviceProvider);
            iterationState.Internals.Plugins.SetState(plugin.GetType(), state);
        }
    }
}
