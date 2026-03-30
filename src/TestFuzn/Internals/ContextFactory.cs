using Fuzn.TestFuzn.Contracts.Adapters;
using Fuzn.TestFuzn.Internals.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fuzn.TestFuzn.Internals;

internal class ContextFactory
{
    public static Context CreateContext(
        TestSession testSession,
        IServiceProvider serviceProvider,
        ITestFrameworkAdapter testFramework,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var context = new Context();

        if (testSession.Configuration != null)
        {
            context.IterationState = new();
            PopulateIterationStateProperties(context.IterationState, testSession, serviceProvider, testFramework, cancellationToken);
            context.StepInfo = new StepInfo(null, stepName, null, null);
        }

        return context;
    }

    public static ScenarioContext CreateScenarioContext(
        TestSession testSession,
        IServiceProvider serviceProvider,
        ITestFrameworkAdapter testFramework,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var context = new ScenarioContext();

        if (testSession.Configuration != null)
        {
            context.IterationState = new();
            PopulateIterationStateProperties(context.IterationState, testSession, serviceProvider, testFramework, cancellationToken);
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

    public static IterationState CreateIterationState(
        TestSession testSession,
        IServiceProvider serviceProvider,
        ITestFrameworkAdapter testFramework,
        Scenario scenario, object?
        currentInput,
        CancellationToken cancellationToken = default)
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
        PopulateIterationStateProperties(iterationState, testSession, serviceProvider, testFramework, cancellationToken);
        iterationState.SharedData = new();
        iterationState.Scenario = scenario;
        iterationState.InputData = currentInput;

        return iterationState;
    }

    private static void PopulateIterationStateProperties(
        IterationState iterationState,
        TestSession testSession,
        IServiceProvider serviceProvider,
        ITestFrameworkAdapter testFramework,
        CancellationToken cancellationToken)
    {
        iterationState.Info = new ExecutionInfo();
        iterationState.Info.TestSession = testSession;
        iterationState.Info.CorrelationId = Guid.NewGuid().ToString();
        iterationState.Info.CancellationToken = cancellationToken;
        iterationState.TestFramework = testFramework;
        iterationState.ServiceProvider = serviceProvider;
        iterationState.Internals = new ContextInternals();
        
        foreach (var plugin in iterationState.Info.TestSession.Configuration.ContextPlugins)
        {
            if (!plugin.RequireIterationState)
                continue;

            if (iterationState.Internals.Plugins == null)
                iterationState.Internals.Plugins = new ContextPluginsState();

            var state = plugin.InitIteration(serviceProvider);
            iterationState.Internals.Plugins.SetState(plugin.GetType(), state);
        }
    }
}
