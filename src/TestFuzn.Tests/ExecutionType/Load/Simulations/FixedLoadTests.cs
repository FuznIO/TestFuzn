namespace Fuzn.TestFuzn.Tests.ExecutionType.Load.Simulations;

[TestClass]
public class FixedLoadTests : TestBase
{
    [Test]
    public async Task Test()
    {
        var stepExecutionCounter = 0;

        await Scenario("Fixed Load Test")
            .Step("Test", context =>
            {
                Interlocked.Increment(ref stepExecutionCounter);

                return Task.CompletedTask;
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(1000, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)))
            .Run();

        Assert.AreEqual(3000, stepExecutionCounter);
    }
}
