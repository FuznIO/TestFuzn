
namespace Fuzn.TestFuzn.Tests.ContextType;

[FeatureTest]
public class ContextTests : BaseFeatureTest
{
    public override Task InitScenarioTest(TestFuzn.Context context)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
        Assert.AreEqual("InitScenarioTest", context.CurrentStep.Name);
        return Task.CompletedTask;
    }

    public override Task CleanupScenarioTest(TestFuzn.Context context)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
        Assert.AreEqual("CleanupScenarioTest", context.CurrentStep.Name);
        return Task.CompletedTask;
    }

    [ScenarioTest]
    public async Task VerifyContext()
    {
        await Scenario()
            .Init(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.AreEqual("Init", context.CurrentStep.Name);
            })
            .Step("Step 1", context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("Step 1", context.CurrentStep.Name);
            })
            .CleanupAfterEachIteration(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("CleanupAfterEachIteration", context.CurrentStep.Name);
            })
            .CleanupAfterScenario(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.AreEqual("CleanupAfterScenario", context.CurrentStep.Name);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }
}
