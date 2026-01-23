namespace Fuzn.TestFuzn.Tests.Steps;

[TestClass]
public class SkipTests : Test
{
    [Test]
    [Skip]
    public async Task VerifySkip()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .AssertInternalState(state => 
            {
                Assert.IsTrue(state.TestExecutionState.TestClassInstance.TestInfo.Skipped);
                Assert.IsTrue(state.TestExecutionState.TestClassInstance.TestInfo.HasSkipAttribute);
                Assert.IsTrue(string.IsNullOrEmpty(state.TestExecutionState.TestClassInstance.TestInfo.SkipReason));
            })
            .Run();
    }

    [Test]
    [Skip("Test is skipped due to...")]
    public async Task VerifySkipWithReason()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                await Task.CompletedTask;
            })
            .AssertInternalState(state => 
            {
                Assert.IsTrue(state.TestExecutionState.TestClassInstance.TestInfo.Skipped);
                Assert.IsTrue(state.TestExecutionState.TestClassInstance.TestInfo.HasSkipAttribute);
                Assert.IsFalse(string.IsNullOrEmpty(state.TestExecutionState.TestClassInstance.TestInfo.SkipReason));
            })
            .Run();
    }
}