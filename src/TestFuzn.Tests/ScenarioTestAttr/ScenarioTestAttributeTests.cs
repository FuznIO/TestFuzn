namespace Fuzn.TestFuzn.Tests.ScenarioTestAttr;

[FeatureTest]
public class ScenarioTestAttributeTests : BaseFeatureTest
{
    [TestMethod]
    public async Task ShouldFail_MS_Test_Method_Is_Not_Supported()
    {
        var wasRun = false;

        await Scenario()
            .Step("Step1", context =>
            {
                wasRun = true;
            })
            .Run();

        Assert.IsFalse(wasRun);
    }

    [ScenarioTest]
    public async Task ScenarioTest_DefaultConstructor_Should_Execute_Scenario()
    {
        var wasRun = false;

        await Scenario()
            .Step("Step1", context =>
            {
                wasRun = true;
            })
            .Run();

        Assert.IsTrue(wasRun);
    }

    [ScenarioTest(ScenarioRunMode.Skip)]
    public async Task ScenarioTest_Skip_ShouldOnlyAddToReportNotExecute()
    {
        var wasRun = false;

        await Scenario()
            .Step("Step1", context =>
            {
                wasRun = true;
            })
            .Run();

        Assert.IsTrue(wasRun);
    }

    [ScenarioTest(ScenarioRunMode.Skip)]
    public async Task SetSkipOnScenario()
    {
        var wasRun = false;

        await Scenario()
            .Skip()
            .Step("Step1", context =>
            {
                wasRun = true;
            })
            .Run();

        Assert.IsFalse(wasRun);
    }
}
