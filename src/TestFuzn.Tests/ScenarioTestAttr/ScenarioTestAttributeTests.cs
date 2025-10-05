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

    [ScenarioTest(ScenarioRunMode.Ignore)]
    public async Task ScenarioTest_Ignore_ShouldOnlyAddToReportButExecute()
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

    [ScenarioTest(ScenarioRunMode.Ignore)]
    public async Task SetRunModeOnScenario_Execute()
    {
        var wasRun = false;

        await Scenario()
            .RunMode(ScenarioRunMode.Execute)
            .Step("Step1", context =>
            {
                wasRun = true;
            })
            .Run();

        Assert.IsTrue(wasRun);
    }

    [ScenarioTest(ScenarioRunMode.Ignore)]
    public async Task SetRunModeOnScenario_Ignore()
    {
        var wasRun = false;

        await Scenario()
            .RunMode(ScenarioRunMode.Ignore)
            .Step("Step1", context =>
            {
                wasRun = true;
            })
            .Run();

        Assert.IsFalse(wasRun);
    }
}
