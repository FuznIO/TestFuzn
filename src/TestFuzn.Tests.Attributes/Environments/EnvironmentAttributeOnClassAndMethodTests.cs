namespace Fuzn.TestFuzn.Tests.Attributes.Environments;


[TestClass]
[Tags("integration")]
[TargetEnvironments("doesntexist")]
public class EnvironmentAttributeOnClassAndMethodTests : Test
{
    [Test]
    [TargetEnvironments("test")]
    [Tags("TagInclude1")]
    public async Task TestShouldRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
                Assert.AreEqual("test", context.Info.TargetEnvironment);
            });
    }

    [Test]
    [Tags("TagInclude1")]
    public async Task TestShouldNotRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
            });
    }
}