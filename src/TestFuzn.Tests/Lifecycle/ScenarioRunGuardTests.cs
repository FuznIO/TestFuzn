using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn.Tests.Lifecycle;

[TestClass]
[Tags("integration")]
public class ScenarioRunGuardTests : Test
{
    [Test]
    [Tags("load")]
    public async Task Scenario_that_is_built_but_not_awaited_is_reported()
    {
        Scenario("Not awaited").Step("Step 1", (context) => { });
        await Scenario("Awaited").Step("Step 1", (context) => { });

        var error = ScenarioRunGuard.GetNotRunError(TestInfo);

        // Clear the list so the guard does not fail this test after it ends.
        TestInfo.BuiltScenarios.Clear();

        Assert.IsNotNull(error);
        Assert.Contains("'Not awaited'", error);
        Assert.DoesNotContain("'Awaited'", error);
    }

    [Test]
    [Tags("load")]
    public async Task Included_scenario_is_not_reported()
    {
        var included = Scenario("Included").Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(1));

        await Scenario("Main").Step("Step 1", (context) => { })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(1))
            .Load().IncludeScenario(included);

        Assert.IsNull(ScenarioRunGuard.GetNotRunError(TestInfo));
    }

    [Test]
    public void ShouldFail_Scenario_that_throws_while_built_is_not_reported()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            Scenario().Step("", (context) => { });
        });

        Assert.IsNull(ScenarioRunGuard.GetNotRunError(TestInfo));
    }

    [Test]
    public async Task ShouldFail_Scenario_that_fails_is_not_reported()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario().Step("Step 1", (context) => throw new InvalidOperationException("Step failure"));
        });

        Assert.IsNull(ScenarioRunGuard.GetNotRunError(TestInfo));
    }
}
