using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn;

internal class AssertInternalState
{
    public TestExecutionState TestExecutionState { get; }

    public AssertInternalState(TestExecutionState testExecutionState)
    {
        TestExecutionState = testExecutionState ?? throw new ArgumentNullException(nameof(testExecutionState));
    }
}
