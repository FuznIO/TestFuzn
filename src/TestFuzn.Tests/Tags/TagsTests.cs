namespace Fuzn.TestFuzn.Tests.Steps;

[TestClass]
[Tags("Tag1_Class")]
public class TagsTests : Test
{
    [Tags("Tag1")]
    [Test]
    public async Task VerifySingleTags()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .AssertInternalState(state => 
            {
                var tags = state.TestExecutionState.TestClassInstance.TestInfo.Tags;
                Assert.Contains("Tag1_Class", tags);
                Assert.Contains("Tag1", tags);
            })
            .Run();
    }

    [Test]
    [Tags("Category1", "Category2", "Category3")]
    public async Task VerifyMultipleTags()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .AssertInternalState(state => 
            {
                var tags = state.TestExecutionState.TestClassInstance.TestInfo.Tags;
                Assert.Contains("Category1", tags);
                Assert.Contains("Category2", tags);
            })
            .Run();
    }
}