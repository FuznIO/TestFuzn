namespace Fuzn.TestFuzn.Tests.TargetEnvironment;

[TestClass]
public class EnvironmentTests : BaseFeatureTest
{
    [Test]
    public async Task Manual_CurrentEnvIsEmpty_ScenarioEnvIsEmpty_ScenarioShouldRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
                Assert.AreEqual("ValuesSectionValue1Root", ConfigurationManager.GetRequiredValue<string>("Value1"));
                Assert.AreEqual("CustomSectionValue1Root", ConfigurationManager.GetRequiredSection<CustomConfigSection>("CustomSection").Value1);
                Assert.IsEmpty(GlobalState.EnvironmentName);
            })
            .Run();
    }

    [Test]
    [Environments("test")]
    public async Task Manual_CurrentEnvIsTest_ScenarioEnvIsTest_ScenarioShouldRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
                Assert.AreEqual("ValuesSectionValue1Test", ConfigurationManager.GetRequiredValue<string>("Value1"));
                Assert.AreEqual("CustomSectionValue1Test", ConfigurationManager.GetRequiredSection<CustomConfigSection>("CustomSection").Value1);
                Assert.AreEqual("test", context.Info.EnvironmentName);
            })
            .Run();
    }
}

public class CustomConfigSection
{
    public string Value1 { get; set; }
}
