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
                Assert.AreEqual("root", context.Configuration.GetRequiredValue<string>("ValueNeverOverride"));
                Assert.AreEqual("dev", context.Configuration.GetRequiredValue<string>("ValueExecOverride"));
                Assert.AreEqual("test", context.Configuration.GetRequiredValue<string>("ValueTargetOverride"));

                Assert.AreEqual("root", context.Configuration.GetRequiredSection<CustomConfigSection>("CustomSection").ValueNeverOverride);
                Assert.AreEqual("dev", context.Configuration.GetRequiredSection<CustomConfigSection>("CustomSection").ValueExecOverride);
                Assert.AreEqual("test", context.Configuration.GetRequiredSection<CustomConfigSection>("CustomSection").ValueTargetOverride);

                Assert.AreEqual("test", context.Info.TargetEnvironment);
                Assert.AreEqual("dev", context.Info.ExecutionEnvironment);
            })
            .Run();
    }
}
