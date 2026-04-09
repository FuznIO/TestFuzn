using System.Collections.Concurrent;
using System.Linq.Expressions;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals;

internal class ContextFactory
{
    private static readonly ConcurrentDictionary<Type, Func<object>> FactoryCache = new();

    private static Func<object> GetOrCreateFactory(Type type)
    {
        return FactoryCache.GetOrAdd(type, t =>
        {
            var newExpr = Expression.New(t);
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(newExpr, typeof(object)));
            return lambda.Compile();
        });
    }

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
            PopulateIterationStateProperties(context.IterationState, testSession, serviceProvider, testFramework, Guid.NewGuid(), cancellationToken);
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
            PopulateIterationStateProperties(context.IterationState, testSession, serviceProvider, testFramework, Guid.NewGuid(), cancellationToken);
            context.StepInfo = new StepInfo(null, stepName, null, null);
        }

        return context;
    }

    public static IterationContext CreateIterationContext(IterationState iterationState, string stepName, string? stepId, string? parentName)
    {
        var factory = GetOrCreateFactory(iterationState.Scenario.ContextType);
        var contextObj = factory();
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
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var iterationState = new IterationState();
        if (scenario.ContextType.IsGenericType && scenario.ContextType.GetGenericTypeDefinition() == typeof(IterationContext<>))
        {
            var modelType = scenario.ContextType.GetGenericArguments()[0];
            var modelFactory = GetOrCreateFactory(modelType);
            var modelInstance = modelFactory();

            if (modelInstance == null)
                throw new InvalidOperationException($"Failed to create instance of {modelType}");

            iterationState.Model = modelInstance;
        }
        PopulateIterationStateProperties(iterationState, testSession, serviceProvider, testFramework, correlationId, cancellationToken);
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
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        iterationState.Info = new ExecutionInfo();
        iterationState.Info.TestSession = testSession;
        iterationState.Info.CorrelationId = correlationId.ToString();
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
