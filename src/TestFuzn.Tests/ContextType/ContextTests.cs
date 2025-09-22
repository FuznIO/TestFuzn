
namespace Fuzn.TestFuzn.Tests.ContextType;

[FeatureTest]
public class ContextTests : BaseFeatureTest
{
    public override Task InitTestMethod(TestFuzn.Context context)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
        Assert.AreEqual("InitScenarioTest", context.StepInfo.Name);
        return Task.CompletedTask;
    }

    public override Task CleanupTestMethod(TestFuzn.Context context)
    {
        Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
        Assert.AreEqual("CleanupScenarioTest", context.StepInfo.Name);
        return Task.CompletedTask;
    }

    [ScenarioTest]
    public async Task VerifyContext()
    {
        await Scenario()
            .InitScenario(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.AreEqual("Init", context.StepInfo.Name);
            })
            .Step("Step 1", context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("Step 1", context.StepInfo.Name);
            })
            .Step("Step 2", "ID2", context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("Step 2", context.StepInfo.Name);
                Assert.AreEqual("ID2", context.StepInfo.Id);
            })
            .CleanupIteration(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("CleanupAfterEachIteration", context.StepInfo.Name);
            })
            .CleanupScenario(context =>
            {
                Assert.IsTrue(!string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.AreEqual("CleanupAfterScenario", context.StepInfo.Name);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }
}
