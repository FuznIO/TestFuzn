namespace Fuzn.TestFuzn.Tests.Steps;

[FeatureTest]
public class CategoriesTests : BaseFeatureTest
{
    public override string FeatureName => "";

    [ScenarioTest]
    [TestCategory("Unit")]
    public async Task UnitTest()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .Run();
    }

    [ScenarioTest]
    [TestCategory("Component")]
    public async Task ComponenTest()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .Run();
    }
    
    [ScenarioTest]
    [TestCategory("EnvA, EnvB")]
    public async Task EnvTest()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .Run();
    }
}