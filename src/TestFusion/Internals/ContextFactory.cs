using TestFusion.Internals.State;
using TestFusion.Plugins.TestFrameworkProviders;

namespace TestFusion.Internals;

internal class ContextFactory
{
    public static Context CreateContext(ITestFrameworkProvider testFramework, string stepName)
    {
        var context = new Context();

        if (GlobalState.Configuration != null)
        {
            PopulateSharedProperties(testFramework, context);
            context.Step = new CurrentStep(null, stepName);
        }

        return context;
    }

    public static StepContext CreateStepContext(ITestFrameworkProvider testFramework, Scenario scenario, string stepName, object currentInput)
    {
        var context = (StepContext) Activator.CreateInstance(scenario.ContextType);

        if (context == null)
            throw new InvalidOperationException($"Failed to create instance of {scenario.ContextType}");

        PopulateSharedProperties(testFramework, context);

        context.Step = new CurrentStep(context, stepName);
        context.Scenario = scenario;
        context.InputDataInternal = currentInput;

        return context;
    }

    private static void PopulateSharedProperties(ITestFrameworkProvider testFramework, Context context)
    {
        context.EnvironmentName = GlobalState.Configuration.EnvironmentName;
        context.TestRunId = GlobalState.TestRunId;
        context.NodeName = GlobalState.NodeName;
        context.Logger = GlobalState.Logger;
        context.TestFramework = testFramework;
        context.CorrelationId = Guid.NewGuid().ToString();
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
