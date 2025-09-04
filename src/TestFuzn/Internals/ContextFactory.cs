using Fuzn.TestFuzn.Internals.State;
using Fuzn.TestFuzn.Contracts.Adapters;

namespace Fuzn.TestFuzn.Internals;

internal class ContextFactory
{
    public static Context CreateContext(ITestFrameworkAdapter testFramework, string stepName)
    {
        var context = new Context();

        if (GlobalState.Configuration != null)
        {
            context.IterationContext = new IterationContext();
            PopulateSharedProperties(testFramework, context.IterationContext);
            context.CurrentStep = new CurrentStep(null, stepName);
        }

        return context;
    }

    public static BaseStepContext CreateStepContext(IterationContext iterationContext, string stepName, string parentName)
    {
        var context = (BaseStepContext) Activator.CreateInstance(iterationContext.Scenario.ContextType);
        if (context == null)
            throw new InvalidOperationException($"Failed to create instance of {iterationContext.Scenario.ContextType}");

        context.IterationContext = iterationContext;
        context.CurrentStep = new CurrentStep(context, stepName, parentName);

        return context;
    }

    public static IterationContext CreateIterationContextForStepContext(ITestFrameworkAdapter testFramework, Scenario scenario, object currentInput)
    {
        var context = new IterationContext();
        if (scenario.ContextType.IsGenericType && scenario.ContextType.GetGenericTypeDefinition() == typeof(StepContext<>))
        {
            var customType = scenario.ContextType.GetGenericArguments()[0];
            var customInstance = Activator.CreateInstance(customType);

            if (customInstance == null)
                throw new InvalidOperationException($"Failed to create instance of {customType}");

            context.Custom = customInstance;
        }
        PopulateSharedProperties(testFramework, context);
        context.SharedData = new();
        context.Scenario = scenario;
        context.InputData = currentInput;

        return context;
    }

    private static void PopulateSharedProperties(ITestFrameworkAdapter testFramework, IterationContext context)
    {
        context.Info = new ExecutionInfo();
        context.Info.EnvironmentName = GlobalState.Configuration.EnvironmentName;
        context.Info.NodeName = GlobalState.NodeName;
        context.Info.TestRunId = GlobalState.TestRunId;
        context.Info.CorrelationId = Guid.NewGuid().ToString();
        context.Logger = GlobalState.Logger;
        context.TestFramework = testFramework;
        context.Internals = new ContextInternals();
        context.SerializerProvider = GlobalState.Configuration.SerializerProviders;
        
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
