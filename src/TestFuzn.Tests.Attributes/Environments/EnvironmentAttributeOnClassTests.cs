namespace Fuzn.TestFuzn.Tests.Attributes.Environments;

[TestClass]
[Tags("integration")]
public class EnvironmentAttributeOnClassTests : Test
{
    [Test]
    [TargetEnvironments("test")]
    [Tags("TagInclude1")]
    public async Task TestShouldRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
            });
    }
}
