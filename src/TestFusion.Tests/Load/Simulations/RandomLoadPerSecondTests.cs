namespace TestFusion.Tests.Load.Simulations;

[FeatureTest]
public class RandomLoadPerSecondTests : BaseFeatureTest
{
    private int _testExecutionCount = 0;

    [ScenarioTest]
    public async Task Test()
    {
        var stepExecutionCount = 0;

        await Scenario("Verify random load")
            .Step("Test", (context) =>
            {
                Interlocked.Increment(ref stepExecutionCount);

                return Task.CompletedTask;
            })
            .Load().Simulations((context, simulations) => simulations.RandomLoadPerSecond(1, 10, TimeSpan.FromSeconds(3)))
            .Run();

        Assert.IsTrue(stepExecutionCount > 0);
    }
}

