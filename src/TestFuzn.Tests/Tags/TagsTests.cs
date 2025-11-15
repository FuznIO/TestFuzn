namespace Fuzn.TestFuzn.Tests.Steps;

[FeatureTest]
public class TagsTests : BaseFeatureTest
{
    public override string FeatureName => "";

    [ScenarioTest]
    [TestCategory("Category1")]
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

    [ScenarioTest]
    [TestCategory("Category1")]
    public async Task VerifyTestCategoriesAndSpecifiedTagsArePartsOfTags()
    {
        await Scenario()
            .Tags("Tag1")
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .AssertInternalState(state => 
            {
                var tags = state.SharedExecutionState.Scenarios.First().TagsInternal;
                Assert.Contains("Category1", tags);
                Assert.Contains("Tag1", tags);
            })
            .Run();
    }

    [ScenarioTest]
    public async Task VerifySpecifiedTags()
    {
        await Scenario()
            .Tags("Tag1", "Tag2")
            .Tags("Tag3")
            .Step("Step 1", async (context) =>
            {    
                await Task.CompletedTask;
            })
            .AssertInternalState(state => 
            {
                var tags = state.SharedExecutionState.Scenarios.First().TagsInternal;
                Assert.Contains("Tag1", tags);
                Assert.Contains("Tag2", tags);
                Assert.Contains("Tag3", tags);
            })
            .Run();
    }
}