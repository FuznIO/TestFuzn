namespace Fuzn.TestFuzn.Tests.Attributes.Environments;


[TestClass]
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
            })
            .Run();
    }

    [Test]
    [Tags("TagInclude1")]
    public async Task TestShouldNotRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
            })
            .Run();
    }
}