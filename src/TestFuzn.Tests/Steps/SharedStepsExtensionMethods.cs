namespace Fuzn.TestFuzn.Tests.Steps;

public static class SharedStepsExtensionMethods
{
    public static ScenarioBuilder<EmptyModel> SharedStepOnOnlyOnStepContextType(
        this ScenarioBuilder<EmptyModel> scenario)
    {
        scenario.Step("SharedStepOnOnlyStepContextType", (context) =>
        {
            context.SetSharedData("SharedStepOnOnlyOnStepContextType_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }

    public static ScenarioBuilder<CustomModel> SharedStepOnOnlyOnCustomStepContextType(
        this ScenarioBuilder<CustomModel> scenario)
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

    public static ScenarioBuilder<TModel> SharedStepOnAllStepContextTypes<TModel>(
        this ScenarioBuilder<TModel> scenario)
        where TModel : new()
    {
        scenario.Step("SharedStepOnAllStepContextTypes", (context) =>
        {
            context.SetSharedData("SharedStepOnAllStepContextTypes_Executed", "true");
            
            return Task.CompletedTask;
        });

        return scenario;
    }
}
