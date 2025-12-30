namespace Fuzn.TestFuzn.Tests.Steps;

[TestClass]
public class TagsTests : TestBase
{
    public override FeatureInfo Feature => new() { Name = "" };

    [Tags("Category1", "Category2", "Category3")]
    [Test]
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

    [Test]
    [Tags("Category1", "Category2", "Category3")]
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