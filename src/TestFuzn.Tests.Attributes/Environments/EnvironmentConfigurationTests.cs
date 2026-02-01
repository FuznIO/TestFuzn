namespace Fuzn.TestFuzn.Tests.Attributes.Environments;

[TestClass]
public class EnvironmentConfigurationTests : Test
{
    [Test]
    [TargetEnvironments("test")]
    [Tags("TagInclude1")]
    public async Task TestShouldRun_ConfigurationShouldHaveExecutionAndTargetOverrides()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
                Assert.AreEqual("root", ConfigurationManager.GetRequiredValue<string>("ValueNeverOverride"));
                Assert.AreEqual("dev", ConfigurationManager.GetRequiredValue<string>("ValueExecOverride"));
                Assert.AreEqual("test", ConfigurationManager.GetRequiredValue<string>("ValueTargetOverride"));

                Assert.AreEqual("root", ConfigurationManager.GetRequiredSection<CustomConfigSection>("CustomSection").ValueNeverOverride);
                Assert.AreEqual("dev", ConfigurationManager.GetRequiredSection<CustomConfigSection>("CustomSection").ValueExecOverride);
                Assert.AreEqual("test", ConfigurationManager.GetRequiredSection<CustomConfigSection>("CustomSection").ValueTargetOverride);

                Assert.AreEqual("test", context.Info.TargetEnvironment);
                Assert.AreEqual("dev", context.Info.ExecutionEnvironment);
            })
            .Run();
    }
}
