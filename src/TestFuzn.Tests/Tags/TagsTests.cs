namespace Fuzn.TestFuzn.Tests.Steps;

[FeatureTest]
public class TagsTests : BaseFeatureTest
{
    public override string FeatureName => "";

    [TestCategory("Category1")]
    [TestCategory("Category2")]
    [TestCategory("Category3")]
    [ScenarioTest]
    public async Task VerifySingleTestCategoryIsTurnedIntoTag()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .AssertInternalState(state => 
            {
                var tags = state.SharedExecutionState.Scenarios.First().TagsInternal;
                Assert.Contains("Category1", tags);
            })
            .Run();
    }

    [ScenarioTest]
    [TestCategory("Category1")]
    [TestCategory("Category2")]
    [TestCategory("Category3")]
    public async Task VerifyMultipleTestCategoriesAreTurnedIntoTags()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .AssertInternalState(state => 
            {
                var tags = state.SharedExecutionState.Scenarios.First().TagsInternal;
                Assert.Contains("Category1", tags);
                Assert.Contains("Category2", tags);
            })
            .Run();
    }
}