namespace Fuzn.TestFuzn.Tests.Steps;

[FeatureTest]
public class InnerStepsTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task InnerSteps_Sync()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                Assert.AreEqual("Step 1", context.CurrentStep.Name);

                context.Step("Step 1.1", (subContext1) =>
                {
                    Assert.AreEqual("Step 1.1", subContext1.CurrentStep.Name);

                    subContext1.Step("Step 1.1.1", (subContext2) =>
                    {
                        Assert.AreEqual("Step 1.1.1", subContext2.CurrentStep.Name);
                    });

                    subContext1.Step("Step 1.1.2", (subContext2) =>
                    {
                        Assert.AreEqual("Step 1.1.2", subContext2.CurrentStep.Name);
                    });
                });

                Assert.IsNotNull(context);
                await Task.CompletedTask;
            })
            .Step("Step 2", (context) =>
            {
                Assert.AreEqual("Step 2", context.CurrentStep.Name);
                context.Step("Step 2.1", (subContext1) =>
                {   
                    Assert.AreEqual("Step 2.1", subContext1.CurrentStep.Name);
                });
            })
            .Run();
    }

    [ScenarioTest]
    public async Task InnerSteps_Async()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                Assert.AreEqual("Step 1", context.CurrentStep.Name);
                await context.Step("Step 1.1", async (subContext1) =>
                {
                    Assert.AreEqual("Step 1.1", subContext1.CurrentStep.Name);
                    await Task.CompletedTask;

                    await context.Step("Step 1.1.1", async (subContext2) =>
                    {   
                        Assert.AreEqual("Step 1.1.1", subContext2.CurrentStep.Name);
                        await Task.CompletedTask;
                    });

                    await context.Step("Step 1.1.2", async (subContext2) =>
                    {   
                        Assert.AreEqual("Step 1.1.2", subContext2.CurrentStep.Name);
                        await Task.CompletedTask;
                    });
                });

                Assert.IsNotNull(context);
                await Task.CompletedTask;
            })
            .Step("Step 2", async (context) =>
            {
                Assert.AreEqual("Step 2", context.CurrentStep.Name);
                await context.Step("Step 2.1", async (subContext1) =>
                {   
                    Assert.AreEqual("Step 2.1", subContext1.CurrentStep.Name);

                    await Task.CompletedTask;
                });
            })
            .Run();
    }
}