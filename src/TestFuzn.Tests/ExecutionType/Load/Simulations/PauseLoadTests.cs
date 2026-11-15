namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Simulations;

[TestClass]
[Tags("integration", "load")]
public class PauseLoadTests : Test
{
    [Test]
    public async Task Test()
    {
        var stepExecutionCount = 0;

        await Scenario("Verify pause")
            .Step("Test", (context) =>
            {
                Interlocked.Increment(ref stepExecutionCount);

                return Task.CompletedTask;
            })
            .Load().Simulations((context, simulations) => simulations.Pause(TimeSpan.FromSeconds(2)));

        Assert.AreEqual(0, stepExecutionCount);
    }
}

