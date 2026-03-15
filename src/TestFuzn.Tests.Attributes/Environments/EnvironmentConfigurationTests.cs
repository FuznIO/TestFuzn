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
                Assert.AreEqual("test", context.Info.TargetEnvironment);
                Assert.AreEqual("dev", context.Info.ExecutionEnvironment);

                Assert.AreEqual("root", context.AppConfiguration.GetRequiredValue<string>("ValueNeverOverride"));
                Assert.AreEqual("dev", context.AppConfiguration.GetRequiredValue<string>("ValueExecOverride"));
                Assert.AreEqual("test", context.AppConfiguration.GetRequiredValue<string>("ValueTargetOverride"));

                Assert.AreEqual("root", context.AppConfiguration.GetRequiredSection<CustomConfigSection>("CustomSection").ValueNeverOverride);
                Assert.AreEqual("dev", context.AppConfiguration.GetRequiredSection<CustomConfigSection>("CustomSection").ValueExecOverride);
                Assert.AreEqual("test", context.AppConfiguration.GetRequiredSection<CustomConfigSection>("CustomSection").ValueTargetOverride);

                Assert.AreEqual("test", context.Info.TargetEnvironment);
                Assert.AreEqual("dev", context.Info.ExecutionEnvironment);
            })
            .Run();
    }
}
