
namespace Fuzn.TestFuzn.Tests.ContextType;

[TestClass]
public class ContextTests : Test, IBeforeTest, IAfterTest
{
    public Task BeforeTest(Context context)
    {
        Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
        Assert.AreEqual("BeforeTest", context.StepInfo.Name);
        return Task.CompletedTask;
    }

    public Task AfterTest(Context context)
    {
        Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
        Assert.AreEqual("AfterTest", context.StepInfo.Name);
        return Task.CompletedTask;
    }

    [Test]
    public async Task VerifyContext()
    {
        await Scenario()
            .BeforeScenario(context =>
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.AreEqual("BeforeScenario", context.StepInfo.Name);
            })
            .BeforeIteration(context =>             
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("BeforeIteration", context.StepInfo.Name);
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
            .AfterIteration(context =>
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.CorrelationId));
                Assert.AreEqual("AfterIteration", context.StepInfo.Name);
            })
            .AfterScenario(context =>
            {
                Assert.IsFalse(string.IsNullOrEmpty(context.Info.TestRunId));
                Assert.AreEqual("AfterScenario", context.StepInfo.Name);
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(10))
            .Run();
    }
}
