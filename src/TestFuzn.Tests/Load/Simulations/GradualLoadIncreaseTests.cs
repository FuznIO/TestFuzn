namespace Fuzn.TestFuzn.Tests.Load.Simulations;

[TestClass]
public class GradualLoadIncreaseTests : TestBase
{
    [Test]
    public async Task GradualLoadIncreaseTestsInitialize()
    {
        var stepExecutionCounter = 0;

        await Scenario("Gradual Load Increase Test")
            .Step("Test", (context) =>
            {
                Interlocked.Increment(ref stepExecutionCounter);

                return Task.CompletedTask;
            })
            .Load().Simulations((context, simulations) => simulations.GradualLoadIncrease(5, 50, TimeSpan.FromSeconds(5)))
            .Run();

        Assert.IsTrue(stepExecutionCounter > 0);
    }
}
