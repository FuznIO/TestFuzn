namespace Fuzn.TestFuzn.Tests.ExecutionType.Load;

[TestClass]
public class MultipleScenariosTests : Test
{
    [Test]
    [Metadata("MetaKey1", "MetaValue2")]
    [Metadata("MetaKey2", "MetaValue2")]
    public async Task Test_multiple_scenarios()
    {
        var scenario1Executed = false;
        var scenario2Executed = false;

        var scenario2 = Scenario("Second scenario")
            .Id("Scenario-2")
            .Step("Step 1", (context) =>
            {
                scenario2Executed = true;
            })
            .Step("Step 2", async (context) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(30));

        await Scenario("First scenario")
            .Id("Scenario-1")
            .Step("Step 1", (context) =>
            {
                scenario1Executed = true;
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(20))
            .Load().IncludeScenario(scenario2)
            .Run();

        Assert.IsTrue(scenario1Executed);
        Assert.IsTrue(scenario2Executed);
    }
}
