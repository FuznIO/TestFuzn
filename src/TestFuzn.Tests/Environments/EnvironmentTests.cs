namespace Fuzn.TestFuzn.Tests.TargetEnvironment;

[TestClass]
public class EnvironmentTests : Test
{
    [Test]
    [TargetEnvironments("", "test")]
    public async Task ShouldRun()
    {
        await Scenario()
            .Step("Step 1", context => 
            {
            })
            .Run();
    }
}
