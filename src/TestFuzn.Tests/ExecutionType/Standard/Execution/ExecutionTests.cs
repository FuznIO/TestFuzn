namespace Fuzn.TestFuzn.Tests.ExecutionType.Standard.Execution;

[TestClass]
[Tags("integration")]
public class ExecutionTests : Test
{
    [Test]
    public async Task Verify_Run_Once()
    {
        var executionCount = 0;

        await Scenario("Verify that a standard test scenario is run only once")
            .Step("Step 1", (context) => 
            {
                Interlocked.Increment(ref executionCount);
            });

        Assert.AreEqual(1, executionCount);
    }

    [Test]
    public async Task Method_name_is_the_test_name()
    {
        await Scenario()
            .Step("Step 1", (context) => { });

        Assert.AreEqual("Method name is the test name", TestInfo.Name);
    }

    [Test]
    public async Task Scenario_name_and_description_name_the_test()
    {
        await Scenario("User can log in with a valid password")
            .Description("Checks the happy path")
            .Step("Step 1", (context) => { });

        Assert.AreEqual("User can log in with a valid password", TestInfo.Name);
        Assert.AreEqual("Checks the happy path", TestInfo.Description);
    }

    [Test(Name = "Name from the attribute", Description = "Description from the attribute")]
    public async Task Attribute_wins_over_scenario()
    {
        await Scenario("Name from the scenario")
            .Description("Description from the scenario")
            .Step("Step 1", (context) => { });

        Assert.AreEqual("Name from the attribute", TestInfo.Name);
        Assert.AreEqual("Description from the attribute", TestInfo.Description);
    }
}
