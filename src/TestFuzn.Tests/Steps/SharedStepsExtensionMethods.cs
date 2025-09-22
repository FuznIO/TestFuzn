namespace Fuzn.TestFuzn.Tests.Steps;

public static class SharedStepsExtensionMethods
{
    public static ScenarioBuilder<IterationContext<EmptyModel>> SharedStepOnOnlyOnStepContextType(
        this ScenarioBuilder<IterationContext<EmptyModel>> scenario)
    {
        scenario.Step("SharedStepOnOnlyStepContextType", (context) =>
        {
            context.SetSharedData("SharedStepOnOnlyOnStepContextType_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }

    public static ScenarioBuilder<IterationContext<CustomStepContext>> SharedStepOnOnlyOnCustomStepContextType(
        this ScenarioBuilder<IterationContext<CustomStepContext>> scenario)
    {
        scenario.Step("SharedStepOnOnlyOnCustomStepContextType", (context) =>
        {
            Assert.AreEqual("Step2", context.Model.CustomProperty);
            context.Model.CustomProperty = "Step3";
            context.SetSharedData("SharedStepOnOnlyOnCustomStepContextType_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }

    public static ScenarioBuilder<TStepContext> SharedStepOnAllStepContextTypes<TStepContext>(
        this ScenarioBuilder<TStepContext> scenario)
        where TStepContext : IterationContext
    {
        scenario.Step("SharedStepOnAllStepContextTypes", (context) =>
        {
            context.SetSharedData("SharedStepOnAllStepContextTypes_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }
}
