using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn.Tests.Ids;

[TestClass]
[Tags("integration")]
public class IdTests : Test
{
    [Test]
    public async Task Verify_a_test_without_an_id_gets_one_from_its_full_name()
    {
        await Scenario()
            .Step("Step 1", context => { })
            .AssertInternalState(state =>
            {
                var testInfo = state.TestExecutionState.TestClassInstance.TestInfo;
                Assert.AreEqual(StableId.Of(testInfo.FullName), testInfo.Id);
                Assert.HasCount(12, testInfo.Id!);
                Assert.IsTrue(testInfo.IsIdAutomatic);
                Assert.AreEqual(testInfo.Id, state.TestExecutionState.TestResult.Id);
            });
    }

    [Test(Id = "ids-test-set-by-hand")]
    public async Task Verify_a_test_id_set_by_hand_is_kept()
    {
        await Scenario()
            .Step("Step 1", context => { })
            .AssertInternalState(state =>
            {
                var testInfo = state.TestExecutionState.TestClassInstance.TestInfo;
                Assert.AreEqual("ids-test-set-by-hand", testInfo.Id);
                Assert.IsFalse(testInfo.IsIdAutomatic);
            });
    }

    [Test]
    public async Task Verify_steps_get_ids_from_their_parent_and_keep_ids_set_by_hand()
    {
        string? stepId = null;
        string? subStepId = null;
        string? manualStepId = null;

        await Scenario()
            .Step("Open page", context =>
            {
                stepId = context.StepInfo.Id;
                context.Step("Read title", sub => { subStepId = sub.StepInfo.Id; });
            })
            .Step("Log in", "login", context => { manualStepId = context.StepInfo.Id; })
            .AssertInternalState(state =>
            {
                var testId = state.TestExecutionState.TestClassInstance.TestInfo.Id!;
                Assert.AreEqual(StableId.Of(testId, "Open page"), stepId);
                Assert.AreEqual(StableId.Of(stepId!, "Read title"), subStepId);
                Assert.AreEqual("login", manualStepId);

                var steps = state.TestExecutionState.TestResult.IterationResults[0].StepResults;
                Assert.AreEqual(stepId, steps["Open page"].Id);
                Assert.IsTrue(steps["Open page"].IsIdAutomatic);
                Assert.AreEqual(subStepId, steps["Open page"].StepResults[0].Id);
                Assert.AreEqual("login", steps["Log in"].Id);
                Assert.IsFalse(steps["Log in"].IsIdAutomatic);
            });
    }

    [Test]
    public async Task ShouldFail_two_steps_with_the_same_id_stop_the_test()
    {
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Scenario()
                .Step("Log in", "login", context => { })
                .Step("Log out", "login", context => { });
        });

        Assert.Contains("have the same id 'login'", exception.Message);
    }
}
