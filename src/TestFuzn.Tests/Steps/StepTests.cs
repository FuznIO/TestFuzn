namespace Fuzn.TestFuzn.Tests.Steps;

[FeatureTest]
public class StepTests : BaseFeatureTest
{
    public override string FeatureName => "";

    [ScenarioTest]
    public async Task Verify_Step_Syntax_For_Scenario_With_Default_StepContext()
    {
        bool step1Executed = false;
        bool step2Executed = false;
        bool step3Executed = false;
        bool step4Executed = false;

        await Scenario()
            .Step("Step 1 - Async with context", async (context) =>
            {
                Assert.IsNotNull(context);
                step1Executed = true;
                await Task.CompletedTask;

            })
            .Step("Step 2 - Sync with context", (context) =>
            {
                Assert.IsNotNull(context);
                step2Executed = true;
            })
            .SharedStepOnOnlyOnStepContextType()
            .Step("Step 3 - Verify that SharedStepOnOnlyOnStepContextType was executed", (context) =>
            {
                Assert.AreEqual("true", context.GetSharedData<string>("SharedStepOnOnlyOnStepContextType_Executed"));
                step3Executed = true;
            })
            .SharedStepOnAllStepContextTypes()
            .Step("Step 4 - Verify that SharedStepOnAllStepContextTypes was executed", (context) =>
            {
                Assert.AreEqual("true", context.GetSharedData<string>("SharedStepOnAllStepContextTypes_Executed"));
                step4Executed = true;
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(1))
            .Run();

        Assert.IsTrue(step1Executed);
        Assert.IsTrue(step2Executed);
        Assert.IsTrue(step3Executed);
        Assert.IsTrue(step4Executed);
    }

    [ScenarioTest]
    public async Task Verify_Step_Syntax_For_Scenario_With_CustomStepContext()
    {
        bool step1Executed = false;
        bool step2Executed = false;
        bool step3Executed = false;
        bool step4Executed = false;

        await Scenario<CustomStepContext>()
            .Step("Step 1 - Async with context", async (context) =>
            {
                Assert.IsNotNull(context);
                Assert.IsNotNull(context.Custom);
                context.Custom.CustomProperty = "Step1";
                step1Executed = true;
                await Task.CompletedTask;

            })
            .Step("Step 2 - Sync with context", (context) =>
            {
                Assert.IsNotNull(context);
                Assert.IsNotNull(context.Custom);
                Assert.AreEqual("Step1", context.Custom.CustomProperty);
                context.Custom.CustomProperty = "Step2";
                step2Executed = true;
            })
            .SharedStepOnOnlyOnCustomStepContextType()
            .Step("Step 4 - Verify that SharedStepOnOnlyOnStepContextType was executed", (context) =>
            {
                Assert.AreEqual("Step3", context.Custom.CustomProperty);
                context.Custom.CustomProperty = "Step4";
                Assert.AreEqual("true", context.GetSharedData<string>("SharedStepOnOnlyOnCustomStepContextType_Executed"));
                step3Executed = true;
            })
            .SharedStepOnAllStepContextTypes()
            .Step("Step 6 - Verify that SharedStepOnAllStepContextTypes was executed", (context) =>
            {
                Assert.AreEqual("Step4", context.Custom.CustomProperty);
                Assert.AreEqual("true", context.GetSharedData<string>("SharedStepOnAllStepContextTypes_Executed"));
                step4Executed = true;
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(1))
            .Run();

        Assert.IsTrue(step1Executed);
        Assert.IsTrue(step2Executed);
        Assert.IsTrue(step3Executed);
        Assert.IsTrue(step4Executed);
    }

    [ScenarioTest]
    public async Task StepNameCannotBeEmpty()
    {
        try
        {
            await Scenario()
                .Step("", (context) => { })
                .Run();

            Assert.Fail();
        }
        catch (Exception ex)
        {
            Assert.AreEqual("Step name cannot be null or empty.", ex.Message);
        }
    }

    [ScenarioTest]
    public async Task StepNameMustBeUnique()
    {
        try
        {
            await Scenario()
                .Step("Step1", (context) => { })
                .Step("Step1", (context) => { })
                .Run();

            Assert.Fail();
        }
        catch (Exception ex)
        {
            Assert.AreEqual("A step with the name 'Step1' already exists in the scenario.", ex.Message);
        }
    }
}