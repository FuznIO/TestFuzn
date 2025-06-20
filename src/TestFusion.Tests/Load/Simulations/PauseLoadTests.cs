namespace TestFusion.Tests.Load.Simulations;

[FeatureTest]
public class PauseLoadTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Test()
    {
        var stepExecutionCount = 0;

        await Scenario("Verify pause")
            .Step("Test", (context) =>
            {
                Interlocked.Increment(ref stepExecutionCount);

                return Task.CompletedTask;
            })
            .Load().Simulations((context, simulations) => simulations.Pause(TimeSpan.FromSeconds(2)))
            .Run();

        Assert.AreEqual(0, stepExecutionCount);
    }
}

