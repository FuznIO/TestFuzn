
namespace Fuzn.TestFuzn.Tests.ContextType;

[TestClass]
public class ContextTests : TestBase, ISetupTest, ITeardownTest
{
    public Task SetupTest(Context context)
    {
        Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
        Assert.AreEqual("InitScenarioTestMethod", context.StepInfo.Name);
        return Task.CompletedTask;
    }

    public Task TeardownTest(Context context)
    {
        Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
        Assert.AreEqual("CleanupScenarioTestMethod", context.StepInfo.Name);
        return Task.CompletedTask;
    }

    [Test]
    public async Task VerifyContext()
    {
        await Scenario()
            .SetupScenario(context =>
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.AreEqual("InitScenario", context.StepInfo.Name);
            })
            .SetupIteration(context =>             
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("InitIteration", context.StepInfo.Name);
            })
            .Step("Step 1", context =>
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("Step 1", context.StepInfo.Name);
            })
            .Step("Step 2", "ID2", context =>
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("Step 2", context.StepInfo.Name);
                Assert.AreEqual("ID2", context.StepInfo.Id);
            })
            .TeardownIteration(context =>
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("CleanupIteration", context.StepInfo.Name);
            })
            .TeardownScenario(context =>
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.AreEqual("CleanupScenario", context.StepInfo.Name);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }
}
