namespace Fuzn.TestFuzn.Tests.Attributes.Tags;

[TestClass]
[Tags("Tag3")]
public class TagsAttributeIncludeOnMethodTests : Test
{
    [Test]
    [TargetEnvironments("test")]
    [Tags("TagInclude1", "Tag2")]
    public async Task TestShouldRun()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .AssertInternalState(state =>
            {
                Assert.Contains("TagInclude1", state.TestExecutionState.TestClassInstance.TestInfo.Tags);
                Assert.Contains("Tag2", state.TestExecutionState.TestClassInstance.TestInfo.Tags);
                Assert.Contains("Tag3", state.TestExecutionState.TestClassInstance.TestInfo.Tags);
                
            })
            .Run();
    }

    [Test]
    [TargetEnvironments("test")]
    public async Task TestShouldNotRun()
    {
        Assert.Fail("This test should not have run because it lacks the required tag.");
    }
}