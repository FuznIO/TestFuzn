namespace Fuzn.TestFuzn.Tests.TargetEnvironment;

[FeatureTest]
public class EnvironmentTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Manual_CurrentEnvIsEmpty_ScenarioEnvIsEmpty_ScenarioShouldRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
                Assert.IsEmpty(GlobalState.EnvironmentName);
            })
            .Run();
    }

    [ScenarioTest]
    [Environments("test")]
    public async Task Manual_CurrentEnvIsTest_ScenarioEnvIsTest_ScenarioShouldRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
                Assert.AreEqual("test", context.Info.EnvironmentName);
            })
            .Run();
    }
}
