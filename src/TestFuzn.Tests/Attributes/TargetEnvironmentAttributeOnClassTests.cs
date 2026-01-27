namespace Fuzn.TestFuzn.Tests.TargetEnvironment;

[TestClass]
[TargetEnvironments("test")]
public class TargetEnvironmentAttributeOnClassTests : Test
{
    [Test]
    [TargetEnvironments("test")]
    public async Task Manual_CurrentEnvIsTest_AttributeIsTest_ShouldRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
            })
            .Run();
    }
}
