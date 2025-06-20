
namespace TestFusion.Tests.ContextType;

[FeatureTest]
public class ContextTests : BaseFeatureTest
{
    public override Task InitScenarioTest(TestFusion.Context context)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(context.TestRunId));
        Assert.AreEqual("BeforeEachScenarioTest", context.Step.Name);
        return Task.CompletedTask;
    }

    public override Task CleanupScenarioTest(TestFusion.Context context)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(context.TestRunId));
        Assert.AreEqual("AfterEachScenarioTest", context.Step.Name);
        return Task.CompletedTask;
    }

    [ScenarioTest]
    public async Task VerifyContext()
    {
        await Scenario()
            .Init(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.TestRunId));
                Assert.AreEqual("Init", context.Step.Name);
            })
            .Step("Step 1", context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.TestRunId));
                Assert.IsTrue(!string.IsNullOrEmpty(context.CorrelationId));
                Assert.AreEqual("Step 1", context.Step.Name);
            })
            .CleanupAfterEachIteration(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.TestRunId));
                Assert.IsTrue(!string.IsNullOrEmpty(context.CorrelationId));
                Assert.AreEqual("CleanupAfterEachIteration", context.Step.Name);
            })
            .CleanupAfterScenario(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.TestRunId));
                Assert.AreEqual("CleanupAfterScenario", context.Step.Name);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }
}
