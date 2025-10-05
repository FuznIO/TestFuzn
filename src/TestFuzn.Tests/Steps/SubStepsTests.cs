namespace Fuzn.TestFuzn.Tests.Steps;

[FeatureTest]
public class SubStepsTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task SubSteps_Sync()
    {
        await Scenario()
            .InputData("Testdata1", "Testdata2")
            .Step("Step 1", async (context) =>
            {
                Assert.AreEqual("Step 1", context.StepInfo.Name);
                context.Comment("Comment for step 1");

                context.Step("Step 1.1", (subContext1) =>
                {
                    Assert.AreEqual("Step 1.1", subContext1.StepInfo.Name);
                    subContext1.Comment("Comment for step 1.1");

                    subContext1.Step("Step 1.1.1", (subContext2) =>
                    {
                        Assert.AreEqual("Step 1.1.1", subContext2.StepInfo.Name);
                        subContext2.Comment("Comment for step 1.1.1");
                    });

                    subContext1.Step("Step 1.1.2", (subContext2) =>
                    {
                        Assert.AreEqual("Step 1.1.2", subContext2.StepInfo.Name);
                        subContext2.Comment("Comment for step 1.1.2");
                    });
                });

                Assert.IsNotNull(context);
                await Task.CompletedTask;
            })
            .Step("Step 2", (context) =>
            {
                Assert.AreEqual("Step 2", context.StepInfo.Name);
                context.Comment("Comment for step 2");
                context.Step("Step 2.1", (subContext1) =>
                {   
                    Assert.AreEqual("Step 2.1", subContext1.StepInfo.Name);
                    context.Comment("Comment for step 2.1");
                });
            })
            .Run();
    }

    [ScenarioTest]
    public async Task SubSteps_Async()
    {
        await Scenario()
            .Tags("CategoryA", "CategoryB")
            .InputData("Testdata1", "Testdata2")
            .Step("Step 1", async (context) =>
            {
                Assert.AreEqual("Step 1", context.StepInfo.Name);
                context.Comment("Comment for step 1");
                await context.Step("Step 1.1", async (subContext1) =>
                {
                    Assert.AreEqual("Step 1.1", subContext1.StepInfo.Name);
                    subContext1.Comment("Comment for step 1");

                    await subContext1.Step("Step 1.1.1", async (subContext2) =>
                    {   
                        Assert.AreEqual("Step 1.1.1", subContext2.StepInfo.Name);
                        subContext2.Comment("Comment for step 1.1.1");
                        await Task.CompletedTask;
                    });

                    await subContext1.Step("Step 1.1.2", async (subContext2) =>
                    {   
                        Assert.AreEqual("Step 1.1.2", subContext2.StepInfo.Name);
                        subContext2.Comment("Comment for step 1.1.2");
                        await Task.CompletedTask;
                    });
                });

                Assert.IsNotNull(context);
                await Task.CompletedTask;
            })
            .Step("Step 2", async (context) =>
            {
                Assert.AreEqual("Step 2", context.StepInfo.Name);
                context.Comment("Comment for step 2");
                await context.Step("Step 2.1", async (subContext1) =>
                {   
                    Assert.AreEqual("Step 2.1", subContext1.StepInfo.Name);
                    subContext1.Comment("Comment for step 2.1");

                    await Task.CompletedTask;
                });
            })
            .Run();
    }

    [ScenarioTest]
    public async Task SubSteps_Async_Load()
    {
        await Scenario()
            .Tags("CategoryA")
            .Step("Step 1", async (context) =>
            {
                Assert.AreEqual("Step 1", context.StepInfo.Name);
                await context.Step("Step 1.1", async (subContext1) =>
                {
                    Assert.AreEqual("Step 1.1", subContext1.StepInfo.Name);
                    await Task.CompletedTask;

                    await subContext1.Step("Step 1.1.1", async (subContext2) =>
                    {
                        Assert.AreEqual("Step 1.1.1", subContext2.StepInfo.Name);
                        await Task.CompletedTask;
                    });

                    await subContext1.Step("Step 1.1.2", async (subContext2) =>
                    {
                        Assert.AreEqual("Step 1.1.2", subContext2.StepInfo.Name);
                        await Task.CompletedTask;
                    });
                });

                Assert.IsNotNull(context);
                await Task.CompletedTask;
            })
            .Step("Step 2", async (context) =>
            {
                Assert.AreEqual("Step 2", context.StepInfo.Name);
                await context.Step("Step 2.1", async (subContext1) =>
                {
                    Assert.AreEqual("Step 2.1", subContext1.StepInfo.Name);

                    await Task.CompletedTask;
                });
            })
            .Load().Simulations((context, builder) => builder.OneTimeLoad(50))
            .Run();
    }
}