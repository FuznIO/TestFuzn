using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn;

internal class AssertInternalState
{
    public SharedExecutionState SharedExecutionState { get; }

    public AssertInternalState(SharedExecutionState sharedExecutionState)
    {
        SharedExecutionState = sharedExecutionState ?? throw new ArgumentNullException(nameof(sharedExecutionState));
    }
}
