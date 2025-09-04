namespace Fuzn.TestFuzn.Tests.Steps;

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

    public static ScenarioBuilder<StepContext<CustomStepContext>> SharedStepOnOnlyOnCustomStepContextType(
        this ScenarioBuilder<StepContext<CustomStepContext>> scenario)
    {
        scenario.Step("SharedStepOnOnlyOnCustomStepContextType", (context) =>
        {
            Assert.AreEqual("Step2", context.Custom.CustomProperty);
            context.Custom.CustomProperty = "Step3";
            context.SetSharedData("SharedStepOnOnlyOnCustomStepContextType_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }

    public static ScenarioBuilder<TStepContext> SharedStepOnAllStepContextTypes<TStepContext>(
        this ScenarioBuilder<TStepContext> scenario)
        where TStepContext : BaseStepContext
    {
        scenario.Step("SharedStepOnAllStepContextTypes", (context) =>
        {
            context.SetSharedData("SharedStepOnAllStepContextTypes_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }
}
