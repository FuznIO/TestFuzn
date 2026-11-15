namespace Fuzn.TestFuzn.Tests.Lifecycle;

[TestClass]
[Tags("integration")]
public class HookFailureReportTests : Test
{
    [Test]
    [Timeout(60000)]
    public async Task ShouldFail_BeforeIteration_failure_is_kept_for_the_report()
    {
        string? failedIn = null;
        string? message = null;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .BeforeIteration(context => throw new InvalidOperationException("Login service is down"))
                .Step("Step 1", context => { })
                .AssertInternalState(state =>
                {
                    failedIn = state.TestExecutionState.TestResult.FailedIn;
                    var failure = state.TestExecutionState.TestResult.Failure;
                    if (failure != null)
                        message = failure.Message;
                });
        });

        Assert.AreEqual("BeforeIteration", failedIn);
        Assert.AreEqual("Login service is down", message);
    }
}
