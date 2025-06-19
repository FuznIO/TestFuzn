namespace TestFusion.Tests.Steps;

public static class SharedStepsExtensionMethods
{
    public static ScenarioBuilder<StepContext> SharedStepOnOnlyOnStepContextType(
        this ScenarioBuilder<StepContext> scenario)
    {
        scenario.Step("SharedStepOnOnlyStepContextType", (context) =>
        {
            context.SetSharedData("SharedStepOnOnlyOnStepContextType_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }

    public static ScenarioBuilder<CustomStepContext> SharedStepOnOnlyOnCustomStepContextType(
        this ScenarioBuilder<CustomStepContext> scenario)
    {
        scenario.Step("SharedStepOnOnlyOnCustomStepContextType", (context) =>
        {
            context.SetSharedData("SharedStepOnOnlyOnCustomStepContextType_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }

    public static ScenarioBuilder<TStepContext> SharedStepOnAllStepContextTypes<TStepContext>(
        this ScenarioBuilder<TStepContext> scenario)
        where TStepContext : StepContext, new()
    {
        scenario.Step("SharedStepOnAllStepContextTypes", (context) =>
        {
            context.SetSharedData("SharedStepOnAllStepContextTypes_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }
}
